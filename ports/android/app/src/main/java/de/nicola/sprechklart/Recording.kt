package de.nicola.sprechklart

import android.media.AudioFormat
import android.media.AudioRecord
import android.media.MediaRecorder
import java.util.concurrent.atomic.AtomicBoolean

internal class Recording(private val onLimit: () -> Unit) {
    private val running = AtomicBoolean(false)
    private var audio: AudioRecord? = null
    private var worker: Thread? = null
    private val chunks = ArrayList<ShortArray>()
    @Volatile private var failure: String? = null
    @Suppress("MissingPermission")
    fun start() {
        check(!running.get())
        val min = AudioRecord.getMinBufferSize(16000, AudioFormat.CHANNEL_IN_MONO, AudioFormat.ENCODING_PCM_16BIT)
        check(min > 0) { "Mikrofonformat nicht unterstützt" }
        val recorder = AudioRecord(MediaRecorder.AudioSource.MIC, 16000, AudioFormat.CHANNEL_IN_MONO, AudioFormat.ENCODING_PCM_16BIT, maxOf(min * 2, 12800))
        if (recorder.state != AudioRecord.STATE_INITIALIZED) { recorder.release(); error("Mikrofon nicht verfügbar") }
        try { recorder.startRecording() } catch (e: Exception) { recorder.release(); throw e }
        if (recorder.recordingState != AudioRecord.RECORDSTATE_RECORDING) { recorder.release(); error("Mikrofon konnte nicht starten") }
        chunks.clear(); failure = null; audio = recorder; running.set(true)
        worker = Thread {
            var total = 0
            try {
                val buffer = ShortArray(1600)
                while (running.get() && total < 16000 * 90) {
                    val count = recorder.read(buffer, 0, minOf(buffer.size, 16000 * 90 - total), AudioRecord.READ_BLOCKING)
                    if (count > 0) { chunks.add(buffer.copyOf(count)); total += count }
                    else if (running.get()) error("Mikrofonaufnahme unterbrochen")
                }
                if (total >= 16000 * 90 && running.get()) onLimit()
            } catch (e: Exception) {
                if (running.get()) { failure = e.message ?: "Aufnahmefehler"; onLimit() }
            } finally {
                running.set(false)
                try { recorder.stop() } catch (_: Exception) {}
                recorder.release()
            }
        }.also { it.start() }
    }
    fun stop(discard: Boolean = false): FloatArray {
        running.set(false)
        try { audio?.stop() } catch (_: Exception) {}
        worker?.join(3000)
        check(worker?.isAlive != true) { "Mikrofon beendet sich noch. Bitte erneut versuchen." }
        audio = null; worker = null
        val size = chunks.sumOf { it.size }
        val samples = if (discard) FloatArray(0) else FloatArray(size)
        if (!discard) {
            var index = 0
            chunks.forEach { chunk -> chunk.forEach { samples[index++] = it / 32768f } }
        }
        chunks.clear()
        if (!discard && failure != null) error(failure!!)
        return samples
    }
}

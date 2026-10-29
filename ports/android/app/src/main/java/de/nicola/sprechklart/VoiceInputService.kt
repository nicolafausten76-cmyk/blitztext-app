package de.nicola.sprechklart

import android.Manifest
import android.content.Intent
import android.content.pm.PackageManager
import android.inputmethodservice.InputMethodService
import android.os.Handler
import android.os.Looper
import android.text.InputType
import android.view.View
import android.view.inputmethod.EditorInfo
import android.widget.Button
import android.widget.LinearLayout
import android.widget.TextView
import java.util.concurrent.Executors

class VoiceInputService : InputMethodService() {
    private lateinit var status: TextView
    private lateinit var mic: Button
    private var recording: Recording? = null
    private var busy = false
    private var generation = 0
    private var discardToken: Int? = null
    private val ui = Handler(Looper.getMainLooper())
    private val executor = Executors.newSingleThreadExecutor()
    override fun onCreateInputView(): View {
        val panel = LinearLayout(this).apply { orientation = LinearLayout.VERTICAL; setPadding(24, 16, 24, 16) }
        status = TextView(this).apply { text = "Sprechklar · offline"; textSize = 17f }; panel.addView(status)
        mic = Button(this).apply { text = "Diktieren"; setOnClickListener { try { toggle() } catch (e: Exception) { status.text = e.message } } }; panel.addView(mic)
        panel.addView(Button(this).apply { text = "Abbrechen"; setOnClickListener { cancel(discardResult = true) } })
        panel.addView(Button(this).apply { text = "Andere Tastatur"; setOnClickListener { switchToNextInputMethod(false) } })
        panel.addView(Button(this).apply { text = "Sprechklar öffnen / Ergebnis ansehen"; setOnClickListener {
            startActivity(Intent(this@VoiceInputService, MainActivity::class.java).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))
        } })
        return panel
    }
    private fun password(info: EditorInfo?): Boolean = FieldPolicy.isPassword(info?.inputType)
    override fun onStartInput(info: EditorInfo?, restarting: Boolean) {
        super.onStartInput(info, restarting)
        generation++
        cancelRecording()
        if (::mic.isInitialized) {
            mic.isEnabled = !password(info)
            status.text = if (password(info)) "Keine Spracheingabe in Passwortfeldern." else "Sprechklar · offline"
        }
    }
    override fun onStartInputView(info: EditorInfo?, restarting: Boolean) {
        super.onStartInputView(info, restarting)
        if (::mic.isInitialized) mic.isEnabled = !password(info) && !busy
    }
    private fun toggle() {
        if (busy || password(currentInputEditorInfo)) return
        if (checkSelfPermission(Manifest.permission.RECORD_AUDIO) != PackageManager.PERMISSION_GRANTED) {
            status.text = "Bitte Mikrofon in der Sprechklar-App erlauben."
            startActivity(Intent(this, MainActivity::class.java).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)); return
        }
        if (recording == null) {
            val selected = getSharedPreferences("settings", MODE_PRIVATE).getString("model", "base") ?: "base"
            if (!ModelStore.path(this, selected).exists()) { status.text = "Modell zuerst in Sprechklar laden."; return }
            lateinit var next: Recording
            next = Recording { ui.post { if (recording === next) { try { toggle() } catch (e: Exception) { status.text = e.message } } } }
            next.start(); recording = next; mic.text = "Stoppen"; status.text = "Aufnahme läuft …"
        } else {
            val active = recording ?: return
            recording = null; mic.text = "Diktieren"
            val samples = active.stop()
            val token = generation
            val connection = currentInputConnection
            busy = true; mic.isEnabled = false; status.text = "Erkenne lokal …"
            executor.execute {
                try {
                    val text = Engine.transcribe(applicationContext, samples)
                    ui.post {
                        // Never read surrounding text; never commit to a changed or hidden field.
                        if (discardToken == token) return@post
                        LastResult.text = text
                        if (token == generation && isInputViewShown && !password(currentInputEditorInfo) && currentInputConnection === connection && text.isNotBlank()) {
                            val accepted = connection?.commitText(text, 1) == true
                            status.text = if (accepted) "Text eingefügt." else "Einfügen nicht möglich. Ergebnis in Sprechklar ansehen."
                        } else if (token == generation) status.text = "Ergebnis in Sprechklar ansehen."
                    }
                } catch (e: Exception) { ui.post { if (token == generation) status.text = e.message } }
                finally {
                    samples.fill(0f)
                    ui.post { busy = false; if (::mic.isInitialized) mic.isEnabled = !password(currentInputEditorInfo) }
                }
            }
        }
    }
    private fun cancelRecording() {
        try { recording?.stop(discard = true) } catch (_: Exception) {}
        recording = null
        if (::mic.isInitialized) mic.text = "Diktieren"
    }
    private fun cancel(discardResult: Boolean = false) { if (discardResult) discardToken = generation; generation++; cancelRecording(); if (::status.isInitialized) status.text = "Aufnahme / Einfügen verworfen." }
    override fun onFinishInputView(finishingInput: Boolean) { cancel(); super.onFinishInputView(finishingInput) }
    override fun onFinishInput() { cancel(); super.onFinishInput() }
    override fun onDestroy() { cancel(); executor.shutdownNow(); super.onDestroy() }
}

package de.nicola.sprechklart

import android.content.Context
import java.io.File
import java.net.HttpURLConnection
import java.net.URL
import java.security.MessageDigest
import java.util.Locale

internal object NativeWhisper {
    init { System.loadLibrary("sprechklart") }
    external fun transcribe(path: String, samples: FloatArray, language: String): String
}
internal object Engine {
    // Only one model context in memory across the activity and the IME.
    @Synchronized fun transcribe(context: Context, samples: FloatArray): String {
        val prefs = context.getSharedPreferences("settings", Context.MODE_PRIVATE)
        val model = prefs.getString("model", "base") ?: "base"
        val file = ModelStore.path(context, model)
        check(file.exists()) { "Bitte Sprachmodell zuerst in Sprechklar laden." }
        val language = if (prefs.getBoolean("autoLanguage", false)) "auto" else "de"
        val original = NativeWhisper.transcribe(file.path, samples, language).trim().replace(Regex("\\s+"), " ")
        return replaceTerms(original, prefs.getString("dictionary", "") ?: "")
    }
    fun replaceTerms(text: String, rules: String): String {
        val terms = linkedMapOf<String, String>()
        rules.lines().forEach { line ->
            val split = line.split("=>", limit = 2)
            if (split.size == 2 && split[0].trim().isNotEmpty()) terms[split[0].trim().lowercase(Locale.ROOT)] = split[1].trim()
        }
        if (terms.isEmpty()) return text
        val alternatives = terms.keys.sortedByDescending { it.length }.joinToString("|") { Regex.escape(it) }
        val regex = Regex("(?<![\\p{L}\\p{N}_])(?:$alternatives)(?![\\p{L}\\p{N}_])", RegexOption.IGNORE_CASE)
        return regex.replace(text) { terms[it.value.lowercase(Locale.ROOT)] ?: it.value }
    }
}
internal object ModelStore {
    private val checksums = mapOf("tiny" to "bd577a113a864445d4c299885e0cb97d4ba92b5f", "base" to "465707469ff3a37a2b9b8d8f89f2f99de7299dac")
    fun path(context: Context, model: String): File {
        require(checksums.containsKey(model))
        return File(context.noBackupFilesDir, "ggml-$model.bin")
    }
    @Synchronized fun download(context: Context, model: String, progress: (String) -> Unit) {
        val expected = checksums[model] ?: error("Unbekanntes Modell")
        val dest = path(context, model)
        val temp = File(dest.parentFile, dest.name + ".part")
        val connection = URL("https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-$model.bin").openConnection() as HttpURLConnection
        connection.connectTimeout = 30000
        connection.readTimeout = 30000
        try {
            check(connection.responseCode == 200) { "Download fehlgeschlagen: HTTP " + connection.responseCode }
            val digest = MessageDigest.getInstance("SHA-1")
            connection.inputStream.use { input ->
                temp.outputStream().use { output ->
                    val buffer = ByteArray(81920)
                    var total = 0L
                    var previousMB = -1L
                    while (true) {
                        if (Thread.currentThread().isInterrupted) error("Download abgebrochen")
                        val n = input.read(buffer)
                        if (n == -1) break
                        total += n
                        check(total <= 170_000_000) { "Unerwartete Modellgröße" }
                        digest.update(buffer, 0, n)
                        output.write(buffer, 0, n)
                        val mb = total / 1024 / 1024
                        if (mb != previousMB) { progress("Modell laden: $mb MB"); previousMB = mb }
                    }
                }
            }
            val actual = digest.digest().joinToString("") { "%02x".format(it.toInt() and 255) }
            check(actual == expected) { "Prüfsumme stimmt nicht. Download verworfen." }
            java.nio.file.Files.move(temp.toPath(), dest.toPath(), java.nio.file.StandardCopyOption.REPLACE_EXISTING)
        } finally {
            connection.disconnect()
            temp.delete()
        }
    }
}

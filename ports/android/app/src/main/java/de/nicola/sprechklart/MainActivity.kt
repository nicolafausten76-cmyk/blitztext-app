package de.nicola.sprechklart

import android.Manifest
import android.app.Activity
import android.app.AlertDialog
import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.os.Build
import android.os.Bundle
import android.provider.Settings
import android.view.View
import android.view.WindowInsets
import android.view.inputmethod.InputMethodManager
import android.widget.*
import java.util.concurrent.Executors

internal object LastResult { @Volatile var text: String = "" }
class MainActivity : Activity() {
    private lateinit var status: TextView
    private lateinit var output: EditText
    private lateinit var recordButton: Button
    private lateinit var model: Spinner
    private lateinit var autoLanguage: CheckBox
    private lateinit var dictionary: EditText
    private var recording: Recording? = null
    private var busy = false
    private var generation = 0
    private val executor = Executors.newSingleThreadExecutor()
    private val prefs by lazy { getSharedPreferences("settings", Context.MODE_PRIVATE) }
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val layout = LinearLayout(this).apply { orientation = LinearLayout.VERTICAL; setPadding(28, 28, 28, 28) }
        val scroll = ScrollView(this).apply { addView(layout) }
        scroll.setOnApplyWindowInsetsListener { view, insets ->
            if (Build.VERSION.SDK_INT >= 30) {
                val bars = insets.getInsets(WindowInsets.Type.systemBars())
                view.setPadding(bars.left, bars.top, bars.right, bars.bottom)
            } else view.setPadding(0, insets.systemWindowInsetTop, 0, insets.systemWindowInsetBottom)
            insets
        }
        setContentView(scroll)
        layout.addView(TextView(this).apply { text = "Sprechklar"; textSize = 30f })
        layout.addView(TextView(this).apply { text = "Lokale Spracheingabe · Vorschau 0.1\nKeine API-Gebühren. Keine Audio-Uploads.\nMaximal 90 Sekunden je Aufnahme."; textSize = 16f })
        model = Spinner(this).apply {
            adapter = ArrayAdapter(this@MainActivity, android.R.layout.simple_spinner_dropdown_item, arrayOf("base", "tiny"))
            setSelection(if (prefs.getString("model", "base") == "tiny") 1 else 0)
        }
        layout.addView(TextView(this).apply { text = "Modell: base genauer · tiny schneller" }); layout.addView(model)
        autoLanguage = CheckBox(this).apply { text = "Sprache automatisch erkennen (sonst Deutsch)"; isChecked = prefs.getBoolean("autoLanguage", false) }
        layout.addView(autoLanguage)
        addButton(layout, "Sprachmodell einmalig laden") { confirmDownload() }
        dictionary = EditText(this).apply {
            hint = "Wörterbuch: n acht n => n8n\nEine Regel je Zeile"
            setText(prefs.getString("dictionary", ""))
            minLines = 3; setSingleLine(false)
        }
        layout.addView(dictionary)
        addButton(layout, "Einstellungen speichern") { save(); status.text = "Einstellungen gespeichert." }
        recordButton = addButton(layout, "Aufnahme starten") { toggle() }
        addButton(layout, "Abbrechen") { cancel() }
        output = EditText(this).apply { hint = "Dein Transkript"; minLines = 5; setSingleLine(false); setText(LastResult.text) }
        layout.addView(output)
        addButton(layout, "Text kopieren") {
            (getSystemService(CLIPBOARD_SERVICE) as ClipboardManager).setPrimaryClip(ClipData.newPlainText("Sprechklar", output.text.toString()))
            status.text = "Text kopiert."
        }
        addButton(layout, "Text verwerfen") { output.setText(""); LastResult.text = "" }
        addButton(layout, "Spracheingabe in Android aktivieren") { save(); startActivity(Intent(Settings.ACTION_INPUT_METHOD_SETTINGS)) }
        addButton(layout, "Tastatur / Spracheingabe auswählen") { (getSystemService(INPUT_METHOD_SERVICE) as InputMethodManager).showInputMethodPicker() }
        layout.addView(TextView(this).apply { text = "Für andere Apps: Sprechklar als Spracheingabe auswählen. Passwortfelder sind gesperrt. Diese erste Android-Fassung diktiert wortgetreu; Schreibmodi und Meetings folgen."; textSize = 14f })
        status = TextView(this).apply { text = "Bereit. Zuerst ein Sprachmodell laden."; textSize = 16f; setPadding(0, 24, 0, 24) }
        layout.addView(status)
    }
    private fun addButton(layout: LinearLayout, title: String, action: () -> Unit): Button =
        Button(this).apply {
            text = title
            setOnClickListener { try { action() } catch (e: Exception) { status.text = e.message ?: "Fehler" } }
            layout.addView(this)
        }
    private fun save() {
        prefs.edit().putString("model", model.selectedItem.toString()).putBoolean("autoLanguage", autoLanguage.isChecked).putString("dictionary", dictionary.text.toString()).apply()
    }
    private fun confirmDownload() {
        if (busy || recording != null) return
        save()
        val selected = model.selectedItem.toString()
        val size = if (selected == "tiny") "75" else "142"
        AlertDialog.Builder(this).setTitle("Sprachmodell laden?")
            .setMessage("Einmalig ca. $size MiB von Hugging Face herunterladen. Danach funktioniert Diktieren offline. Es werden keine Aufnahmen hochgeladen.")
            .setNegativeButton("Abbrechen", null)
            .setPositiveButton("Laden") { _, _ ->
                busy = true; val token = ++generation
                executor.execute {
                    try {
                        ModelStore.download(applicationContext, selected) { message -> runOnUiThread { if (token == generation) status.text = message } }
                        runOnUiThread { if (token == generation) status.text = "Modell geprüft. Offline bereit." }
                    } catch (e: Exception) { runOnUiThread { if (token == generation) status.text = e.message } }
                    finally { runOnUiThread { if (token == generation) busy = false } }
                }
            }.show()
    }
    private fun toggle() {
        if (busy) return
        if (recording == null) {
            if (checkSelfPermission(Manifest.permission.RECORD_AUDIO) != PackageManager.PERMISSION_GRANTED) {
                requestPermissions(arrayOf(Manifest.permission.RECORD_AUDIO), 10)
                status.text = "Mikrofon erlauben und Aufnahme erneut starten."; return
            }
            save()
            if (!ModelStore.path(this, model.selectedItem.toString()).exists()) { status.text = "Bitte zuerst Sprachmodell laden."; return }
            lateinit var next: Recording
            next = Recording { runOnUiThread { if (recording === next) { try { toggle() } catch (e: Exception) { status.text = e.message } } } }
            next.start(); recording = next
            recordButton.text = "Aufnahme stoppen"; status.text = "Aufnahme läuft …"
        } else {
            val active = recording ?: return
            recording = null; recordButton.text = "Aufnahme starten"
            val samples = active.stop()
            busy = true; status.text = "Lokale Spracherkennung …"; val token = ++generation
            executor.execute {
                try {
                    val text = Engine.transcribe(applicationContext, samples)
                    runOnUiThread {
                        if (token == generation) { output.setText(text); LastResult.text = text; status.text = if (text.isBlank()) "Keine Sprache erkannt." else "Fertig. Text bleibt auf deinem Gerät." }
                    }
                } catch (e: Exception) { runOnUiThread { if (token == generation) status.text = e.message } }
                finally { samples.fill(0f); runOnUiThread { if (token == generation) busy = false } }
            }
        }
    }
    private fun cancel() {
        // Inference already running may finish in the background, but its result is discarded.
        if (busy) { generation++; busy = false; status.text = "Ergebnis verworfen. Laufende Berechnung kann noch auslaufen."; return }
        recording?.stop(discard = true); recording = null
        recordButton.text = "Aufnahme starten"; status.text = "Aufnahme verworfen."
    }
    override fun onResume() { super.onResume(); if (::output.isInitialized && LastResult.text.isNotBlank()) output.setText(LastResult.text) }
    override fun onStop() {
        try { recording?.stop(discard = true) } catch (_: Exception) {}
        recording = null
        if (::recordButton.isInitialized) recordButton.text = "Aufnahme starten"
        super.onStop()
    }
    override fun onDestroy() { generation++; executor.shutdownNow(); super.onDestroy() }
}

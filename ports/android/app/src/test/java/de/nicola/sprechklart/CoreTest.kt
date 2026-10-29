package de.nicola.sprechklart

import android.text.InputType
import org.junit.Assert.*
import org.junit.Test

class CoreTest {
    @Test fun dictionaryPreservesWordFragmentsAndUnicode() {
        assertEquals("n8n und Banana", Engine.replaceTerms("n acht n und Banana", "n acht n => n8n\nana => X"))
        assertEquals("Äpfel X", Engine.replaceTerms("Äpfel Apfel", "Apfel => X"))
    }
    @Test fun dictionaryDoesNotCascade() {
        assertEquals("B C", Engine.replaceTerms("A B", "A => B\nB => C"))
        assertEquals("LANG", Engine.replaceTerms("KI Beratung", "KI => kurz\nKI Beratung => LANG"))
    }
    @Test fun secretFieldsAreBlocked() {
        assertTrue(FieldPolicy.isPassword(null))
        assertTrue(FieldPolicy.isPassword(InputType.TYPE_NULL))
        for (variation in listOf(InputType.TYPE_TEXT_VARIATION_PASSWORD, InputType.TYPE_TEXT_VARIATION_WEB_PASSWORD, InputType.TYPE_TEXT_VARIATION_VISIBLE_PASSWORD))
            assertTrue(FieldPolicy.isPassword(InputType.TYPE_CLASS_TEXT or variation))
        assertTrue(FieldPolicy.isPassword(InputType.TYPE_CLASS_NUMBER or InputType.TYPE_NUMBER_VARIATION_PASSWORD))
    }
    @Test fun ordinaryFieldsAreAllowed() {
        assertFalse(FieldPolicy.isPassword(InputType.TYPE_CLASS_TEXT))
        assertFalse(FieldPolicy.isPassword(InputType.TYPE_CLASS_NUMBER))
        assertFalse(FieldPolicy.isPassword(InputType.TYPE_CLASS_TEXT or InputType.TYPE_TEXT_VARIATION_EMAIL_ADDRESS))
    }
}

#include <jni.h>
#include <whisper.h>
#include <string>
#include <vector>
#include <memory>
#include <cmath>

static void fail(JNIEnv* env, const char* message) {
    env->ThrowNew(env->FindClass("java/lang/IllegalStateException"), message);
}
extern "C" JNIEXPORT jstring JNICALL
Java_de_nicola_sprechklart_NativeWhisper_transcribe(JNIEnv* env, jobject, jstring path, jfloatArray pcm, jstring language) {
    const char* p = env->GetStringUTFChars(path, nullptr);
    if (!p) return nullptr;
    std::string model(p);
    env->ReleaseStringUTFChars(path, p);
    const char* l = env->GetStringUTFChars(language, nullptr);
    if (!l) return nullptr;
    std::string lang(l);
    env->ReleaseStringUTFChars(language, l);
    jsize count = env->GetArrayLength(pcm);
    if (count < 4800) return env->NewStringUTF("");
    if (count > 16000 * 90) { fail(env, "Aufnahme ist zu lang."); return nullptr; }
    std::vector<float> data(count);
    env->GetFloatArrayRegion(pcm, 0, count, data.data());
    if (env->ExceptionCheck()) return nullptr;
    double power = 0;
    for (float value : data) power += value * value;
    if (std::sqrt(power / count) < 0.001) return env->NewStringUTF("");
    auto cp = whisper_context_default_params();
    cp.use_gpu = false;
    std::unique_ptr<whisper_context, decltype(&whisper_free)> ctx(whisper_init_from_file_with_params(model.c_str(), cp), whisper_free);
    if (!ctx) { fail(env, "Sprachmodell konnte nicht geladen werden."); return nullptr; }
    auto params = whisper_full_default_params(WHISPER_SAMPLING_GREEDY);
    params.n_threads = 4;
    params.language = lang.c_str();
    params.translate = false;
    params.no_context = true;
    params.print_progress = false;
    params.print_realtime = false;
    params.print_timestamps = false;
    if (whisper_full(ctx.get(), params, data.data(), count) != 0) {
        fail(env, "Lokale Spracherkennung fehlgeschlagen."); return nullptr;
    }
    std::string output;
    for (int i = 0; i < whisper_full_n_segments(ctx.get()); ++i)
        output += whisper_full_get_segment_text(ctx.get(), i);
    // Decode normal UTF-8 with Java rather than JNI modified UTF-8.
    jbyteArray bytes = env->NewByteArray(static_cast<jsize>(output.size()));
    if (!bytes) return nullptr;
    env->SetByteArrayRegion(bytes, 0, static_cast<jsize>(output.size()), reinterpret_cast<const jbyte*>(output.data()));
    jclass stringClass = env->FindClass("java/lang/String");
    jmethodID ctor = env->GetMethodID(stringClass, "<init>", "([BLjava/lang/String;)V");
    jstring encoding = env->NewStringUTF("UTF-8");
    return static_cast<jstring>(env->NewObject(stringClass, ctor, bytes, encoding));
}

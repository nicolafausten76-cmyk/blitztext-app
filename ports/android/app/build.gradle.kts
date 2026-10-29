plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
}
android {
    namespace = "de.nicola.sprechklart"
    compileSdk = 36
    ndkVersion = "28.2.13676358"
    defaultConfig {
        applicationId = "de.nicola.sprechklart"
        minSdk = 29
        targetSdk = 36
        versionCode = 1
        versionName = "0.1.0"
        ndk { abiFilters += "arm64-v8a" }
        externalNativeBuild {
            cmake {
                arguments += listOf("-DWHISPER_SOURCE_DIR=" + rootProject.file("vendor/whisper.cpp").absolutePath, "-DANDROID_SUPPORT_FLEXIBLE_PAGE_SIZES=ON")
                cppFlags += "-std=c++17"
            }
        }
    }
    buildTypes { release { isMinifyEnabled = false } }
    externalNativeBuild { cmake { path = file("src/main/cpp/CMakeLists.txt"); version = "3.22.1" } }
    compileOptions { sourceCompatibility = JavaVersion.VERSION_17; targetCompatibility = JavaVersion.VERSION_17 }
    kotlinOptions { jvmTarget = "17" }
    packaging { jniLibs { useLegacyPackaging = false } }
}

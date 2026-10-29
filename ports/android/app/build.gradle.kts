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
        versionCode = 2
        versionName = "0.1.1"
        ndk { abiFilters += "arm64-v8a" }
        externalNativeBuild {
            cmake {
                arguments += listOf("-DWHISPER_SOURCE_DIR=" + rootProject.file("vendor/whisper.cpp").absolutePath, "-DANDROID_SUPPORT_FLEXIBLE_PAGE_SIZES=ON")
                cppFlags += "-std=c++17"
            }
        }
    }
    val keyPath = System.getenv("SPRECHKLAR_KEYSTORE")
    val keyStorePassword = System.getenv("SPRECHKLAR_STORE_PASSWORD")
    val keyAliasValue = System.getenv("SPRECHKLAR_KEY_ALIAS")
    val keyPasswordValue = System.getenv("SPRECHKLAR_KEY_PASSWORD")
    val hasSigningKey = listOf(keyPath, keyStorePassword, keyAliasValue, keyPasswordValue).all { !it.isNullOrBlank() }
    signingConfigs {
        if (hasSigningKey) create("privateRelease") {
            storeFile = file(keyPath!!)
            storePassword = keyStorePassword
            keyAlias = keyAliasValue
            keyPassword = keyPasswordValue
        }
    }
    buildTypes {
        release {
            isDebuggable = false
            isJniDebuggable = false
            isMinifyEnabled = false
            if (hasSigningKey) signingConfig = signingConfigs.getByName("privateRelease")
        }
    }
    externalNativeBuild { cmake { path = file("src/main/cpp/CMakeLists.txt"); version = "3.22.1" } }
    compileOptions { sourceCompatibility = JavaVersion.VERSION_17; targetCompatibility = JavaVersion.VERSION_17 }
    kotlinOptions { jvmTarget = "17" }
    packaging { jniLibs { useLegacyPackaging = false } }
}

dependencies { testImplementation("junit:junit:4.13.2") }

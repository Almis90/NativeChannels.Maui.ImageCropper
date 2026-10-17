plugins {
    id("com.android.library") version "8.13.2"
    id("org.jetbrains.kotlin.android") version "2.3.0"
}
android {
    namespace = "dev.nativechannels.imagecropper"
    compileSdk = 36
    defaultConfig { minSdk = 24; consumerProguardFiles("consumer-rules.pro") }
    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
    testOptions { unitTests.isIncludeAndroidResources = true }
}
kotlin { compilerOptions { jvmTarget.set(org.jetbrains.kotlin.gradle.dsl.JvmTarget.JVM_17) } }
// Extracted from the published NativeChannels.Native.Android package by eng/prepare-native.py.
dependencies {
    compileOnly(files("../../artifacts/dependencies/android/bridge.jar"))
    implementation("com.vanniktech:android-image-cropper:4.7.0")
    testImplementation(files("../../artifacts/dependencies/android/bridge.jar"))
    testImplementation("junit:junit:4.13.2")
    testImplementation("org.robolectric:robolectric:4.14.1")
}

// An AAR does not embed dependencies. Stage CanHub separately; AndroidX/Kotlin are
// supplied by NuGet references in the managed plugin to avoid duplicate Java classes.
val canHubArtifact by configurations.creating {
    isTransitive = false
}
dependencies { add(canHubArtifact.name, "com.vanniktech:android-image-cropper:4.7.0@aar") }
tasks.register<Copy>("stageCanHub") {
    from(canHubArtifact)
    into(layout.buildDirectory.dir("native-dependencies"))
}

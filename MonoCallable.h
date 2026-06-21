#pragma once

#ifdef MONO_BINDINGS
#include <mono/jit/jit.h>
#include <mono/metadata/object.h>

template<typename... Args>
struct MonoCallable {
    uint32_t gchandle_ = 0;

    ~MonoCallable() { Free(); }
    MonoCallable(const MonoCallable&) = delete;
    MonoCallable& operator=(const MonoCallable&) = delete;
    MonoCallable(MonoCallable&& other) noexcept : gchandle_(other.gchandle_) { other.gchandle_ = 0; }
    MonoCallable& operator=(MonoCallable&& other) noexcept {
        if (this != &other) { Free(); gchandle_ = other.gchandle_; other.gchandle_ = 0; }
        return *this;
    }

    void SetMonoDelegate(MonoObject* del) {
        Free();
        if (del) gchandle_ = mono_gchandle_new(del, false);
    }

    void operator()(Args... args) {
        if (!gchandle_) return;
        MonoObject* del = mono_gchandle_get_target(gchandle_);
        if (!del) return;
        void* argPtrs[] = { &args... };
        mono_runtime_delegate_invoke(del, argPtrs, nullptr);
    }

private:
    void Free() {
        if (gchandle_) { mono_gchandle_free(gchandle_); gchandle_ = 0; }
    }
};

#else
template<typename... Args>
struct MonoCallable {
    void SetMonoDelegate(void*) {}
    void operator()(Args...) {}
};
#endif

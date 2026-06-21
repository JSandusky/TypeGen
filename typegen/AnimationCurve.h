#pragma once

#include <vector>
#include <cstddef>
#include <cmath>
#include <algorithm>
#include <cfloat>

static constexpr float kDefaultWeight = 1.0f / 3.0f;
static constexpr float kTangentInfinityThreshold = 1e34f;

enum class WrapMode
{
    Once = 0,
    Loop = 1,
    PingPong = 2,
    Default = Once,
    Clamp = Once,
    ClampForever = 4
};

enum class WeightedMode
{
    None = 0,
    In = 1,
    Out = 2,
    Both = 3
};

struct Keyframe
{
    float time;
    float value;
    float inTangent;
    float outTangent;
    float inWeight;
    float outWeight;
    WeightedMode weightedMode;

    Keyframe()
        : time(0.0f), value(0.0f), inTangent(0.0f), outTangent(0.0f),
          inWeight(kDefaultWeight), outWeight(kDefaultWeight), weightedMode(WeightedMode::None)
    {
    }

    Keyframe(float t, float v)
        : time(t), value(v), inTangent(0.0f), outTangent(0.0f),
          inWeight(kDefaultWeight), outWeight(kDefaultWeight), weightedMode(WeightedMode::None)
    {
    }

    Keyframe(float t, float v, float inTan, float outTan)
        : time(t), value(v), inTangent(inTan), outTangent(outTan),
          inWeight(kDefaultWeight), outWeight(kDefaultWeight), weightedMode(WeightedMode::None)
    {
    }
};

class AnimationCurve
{
public:
    AnimationCurve();

    float Evaluate(float time) const;

    size_t AddKey(const Keyframe& key);
    size_t AddKey(float time, float value);
    bool RemoveKey(size_t index);
    void MoveKey(size_t index, const Keyframe& key);
    void SmoothTangents(size_t index, float weight);

    const Keyframe& GetKey(size_t index) const;
    size_t GetLength() const { return m_Keys.size(); }

    WrapMode GetPreWrapMode() const { return m_PreWrapMode; }
    void SetPreWrapMode(WrapMode mode) { m_PreWrapMode = mode; }
    WrapMode GetPostWrapMode() const { return m_PostWrapMode; }
    void SetPostWrapMode(WrapMode mode) { m_PostWrapMode = mode; }

    const std::vector<Keyframe>& keys() const { return m_Keys; }
    std::vector<Keyframe>& keys() { return m_Keys; }
    void SetKeys(const std::vector<Keyframe>& newKeys);

    static AnimationCurve Constant(float value);
    static AnimationCurve Linear(float timeStart, float valueStart, float timeEnd, float valueEnd);
    static AnimationCurve EaseInOut(float timeStart, float valueStart, float timeEnd, float valueEnd);

    void Clear() { m_Keys.clear(); }

private:
    std::vector<Keyframe> m_Keys;
    WrapMode m_PreWrapMode;
    WrapMode m_PostWrapMode;

    static float HermiteInterpolate(float t, float p0, float p1, float m0, float m1);
    static float BezierInterpolate(float t, float p0, float p1, float c0, float c1);

    int FindSegment(float time) const;
    float EvaluateSegment(int segmentIdx, float time) const;
    void AutoSetTangent(size_t idx);
    void AutoSetTangents();

    static bool IsInfiniteTangent(float val)
    {
        return val >= kTangentInfinityThreshold || val <= -kTangentInfinityThreshold;
    }
};

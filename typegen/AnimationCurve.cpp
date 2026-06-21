#include "AnimationCurve.h"

AnimationCurve::AnimationCurve()
    : m_PreWrapMode(WrapMode::Once)
    , m_PostWrapMode(WrapMode::Once)
{
}

float AnimationCurve::HermiteInterpolate(float t, float p0, float p1, float m0, float m1)
{
    float t2 = t * t;
    float t3 = t2 * t;

    float h00 = 2.0f * t3 - 3.0f * t2 + 1.0f;
    float h10 = t3 - 2.0f * t2 + t;
    float h01 = -2.0f * t3 + 3.0f * t2;
    float h11 = t3 - t2;

    return h00 * p0 + h10 * m0 + h01 * p1 + h11 * m1;
}

float AnimationCurve::BezierInterpolate(float t, float p0, float p1, float c0, float c1)
{
    float u = 1.0f - t;
    float u2 = u * u;
    float t2 = t * t;

    return u2 * u * p0 + 3.0f * u2 * t * c0 + 3.0f * u * t2 * c1 + t2 * t * p1;
}

int AnimationCurve::FindSegment(float time) const
{
    size_t n = m_Keys.size();

    size_t lo = 0;
    size_t hi = n - 1;

    while (lo < hi)
    {
        size_t mid = (lo + hi + 1) / 2;
        if (m_Keys[mid].time <= time)
            lo = mid;
        else
            hi = mid - 1;
    }

    if (lo == n - 1)
        return static_cast<int>(n - 2);

    return static_cast<int>(lo);
}

float AnimationCurve::EvaluateSegment(int segmentIdx, float time) const
{
    const Keyframe& prev = m_Keys[segmentIdx];
    const Keyframe& next = m_Keys[segmentIdx + 1];

    float dt = next.time - prev.time;
    if (dt <= 0.0f)
        return prev.value;

    float t = (time - prev.time) / dt;
    t = std::max(0.0f, std::min(1.0f, t));

    bool prevOutInf = IsInfiniteTangent(prev.outTangent);
    bool nextInInf = IsInfiniteTangent(next.inTangent);

    if (prevOutInf && nextInInf)
        return t < 0.5f ? prev.value : next.value;
    if (prevOutInf)
        return prev.value;
    if (nextInInf)
        return next.value;

    bool hasWeighting =
        (prev.weightedMode == WeightedMode::Out || prev.weightedMode == WeightedMode::Both ||
         next.weightedMode == WeightedMode::In || next.weightedMode == WeightedMode::Both);

    if (hasWeighting)
    {
        float w0 = (prev.weightedMode == WeightedMode::Out || prev.weightedMode == WeightedMode::Both)
                       ? prev.outWeight
                       : kDefaultWeight;
        float w1 = (next.weightedMode == WeightedMode::In || next.weightedMode == WeightedMode::Both)
                       ? next.inWeight
                       : kDefaultWeight;

        float cp0 = prev.value + prev.outTangent * dt * w0;
        float cp1 = next.value - next.inTangent * dt * w1;

        return BezierInterpolate(t, prev.value, next.value, cp0, cp1);
    }
    else
    {
        float m0 = prev.outTangent * dt;
        float m1 = next.inTangent * dt;
        return HermiteInterpolate(t, prev.value, next.value, m0, m1);
    }
}

float AnimationCurve::Evaluate(float time) const
{
    size_t n = m_Keys.size();
    if (n == 0)
        return 0.0f;
    if (n == 1)
        return m_Keys[0].value;

    float firstTime = m_Keys[0].time;
    float lastTime = m_Keys[n - 1].time;

    if (time < firstTime)
    {
        switch (m_PreWrapMode)
        {
        case WrapMode::ClampForever:
            return m_Keys[0].value;
        case WrapMode::Loop:
        {
            float range = lastTime - firstTime;
            if (range <= 0.0f)
                return m_Keys[0].value;
            float t = std::fmod(time - firstTime, range);
            if (t < 0.0f)
                t += range;
            time = firstTime + t;
            break;
        }
        case WrapMode::PingPong:
        {
            float range = lastTime - firstTime;
            if (range <= 0.0f)
                return m_Keys[0].value;
            float t = std::fmod(time - firstTime, range * 2.0f);
            if (t < 0.0f)
                t += range * 2.0f;
            if (t > range)
                t = range * 2.0f - t;
            time = firstTime + t;
            break;
        }
        default:
            time = firstTime;
            break;
        }
    }
    else if (time > lastTime)
    {
        switch (m_PostWrapMode)
        {
        case WrapMode::ClampForever:
            return m_Keys[n - 1].value;
        case WrapMode::Loop:
        {
            float range = lastTime - firstTime;
            if (range <= 0.0f)
                return m_Keys[n - 1].value;
            float t = std::fmod(time - firstTime, range);
            if (t < 0.0f)
                t += range;
            time = firstTime + t;
            break;
        }
        case WrapMode::PingPong:
        {
            float range = lastTime - firstTime;
            if (range <= 0.0f)
                return m_Keys[n - 1].value;
            float t = std::fmod(time - firstTime, range * 2.0f);
            if (t < 0.0f)
                t += range * 2.0f;
            if (t > range)
                t = range * 2.0f - t;
            time = firstTime + t;
            break;
        }
        default:
            time = lastTime;
            break;
        }
    }

    int segIdx = FindSegment(time);
    return EvaluateSegment(segIdx, time);
}

size_t AnimationCurve::AddKey(const Keyframe& key)
{
    auto it = std::lower_bound(
        m_Keys.begin(), m_Keys.end(), key,
        [](const Keyframe& a, const Keyframe& b) { return a.time < b.time; });

    if (it != m_Keys.end() && it->time == key.time)
    {
        size_t idx = static_cast<size_t>(it - m_Keys.begin());
        m_Keys[idx] = key;
        AutoSetTangents();
        return idx;
    }

    size_t idx = static_cast<size_t>(it - m_Keys.begin());
    m_Keys.insert(it, key);
    AutoSetTangents();
    return idx;
}

size_t AnimationCurve::AddKey(float time, float value)
{
    return AddKey(Keyframe(time, value));
}

bool AnimationCurve::RemoveKey(size_t index)
{
    if (index >= m_Keys.size())
        return false;

    m_Keys.erase(m_Keys.begin() + static_cast<ptrdiff_t>(index));
    if (m_Keys.size() > 1)
        AutoSetTangents();
    return true;
}

void AnimationCurve::MoveKey(size_t index, const Keyframe& key)
{
    if (index >= m_Keys.size())
        return;

    m_Keys[index] = key;
    std::sort(
        m_Keys.begin(), m_Keys.end(),
        [](const Keyframe& a, const Keyframe& b) { return a.time < b.time; });
    AutoSetTangents();
}

void AnimationCurve::SmoothTangents(size_t index, float weight)
{
    if (index >= m_Keys.size())
        return;

    if (m_Keys.size() <= 1)
    {
        m_Keys[index].inTangent = 0.0f;
        m_Keys[index].outTangent = 0.0f;
        return;
    }

    float smoothTangent;

    if (index == 0)
    {
        float dt = m_Keys[1].time - m_Keys[0].time;
        smoothTangent = (dt > 0.0f) ? (m_Keys[1].value - m_Keys[0].value) / dt : 0.0f;
    }
    else if (index == m_Keys.size() - 1)
    {
        float dt = m_Keys[index].time - m_Keys[index - 1].time;
        smoothTangent = (dt > 0.0f) ? (m_Keys[index].value - m_Keys[index - 1].value) / dt : 0.0f;
    }
    else
    {
        float dt1 = m_Keys[index].time - m_Keys[index - 1].time;
        float dt2 = m_Keys[index + 1].time - m_Keys[index].time;

        if (dt1 > 0.0f && dt2 > 0.0f)
        {
            float slope1 = (m_Keys[index].value - m_Keys[index - 1].value) / dt1;
            float slope2 = (m_Keys[index + 1].value - m_Keys[index].value) / dt2;
            smoothTangent = (slope1 * dt2 + slope2 * dt1) / (dt1 + dt2);
        }
        else
        {
            smoothTangent = 0.0f;
        }
    }

    m_Keys[index].inTangent = smoothTangent * weight;
    m_Keys[index].outTangent = smoothTangent * weight;
}

const Keyframe& AnimationCurve::GetKey(size_t index) const
{
    return m_Keys[index];
}

void AnimationCurve::SetKeys(const std::vector<Keyframe>& newKeys)
{
    m_Keys = newKeys;

    if (m_Keys.size() > 1)
    {
        std::sort(
            m_Keys.begin(), m_Keys.end(),
            [](const Keyframe& a, const Keyframe& b) { return a.time < b.time; });
        AutoSetTangents();
    }
}

AnimationCurve AnimationCurve::Constant(float value)
{
    AnimationCurve curve;
    curve.m_Keys.push_back({0.0f, value});
    curve.m_Keys.push_back({1.0f, value});
    return curve;
}

AnimationCurve AnimationCurve::Linear(float timeStart, float valueStart, float timeEnd, float valueEnd)
{
    AnimationCurve curve;
    float slope = (timeEnd != timeStart) ? (valueEnd - valueStart) / (timeEnd - timeStart) : 0.0f;
    curve.m_Keys.push_back({timeStart, valueStart, slope, slope});
    curve.m_Keys.push_back({timeEnd, valueEnd, slope, slope});
    return curve;
}

AnimationCurve AnimationCurve::EaseInOut(float timeStart, float valueStart, float timeEnd, float valueEnd)
{
    AnimationCurve curve;
    curve.m_Keys.push_back({timeStart, valueStart, 0.0f, 0.0f});
    curve.m_Keys.push_back({timeEnd, valueEnd, 0.0f, 0.0f});
    return curve;
}

void AnimationCurve::AutoSetTangent(size_t idx)
{
    size_t n = m_Keys.size();
    if (n == 0)
        return;
    if (n == 1)
    {
        m_Keys[0].inTangent = 0.0f;
        m_Keys[0].outTangent = 0.0f;
        return;
    }

    if (idx == 0)
    {
        float dt = m_Keys[1].time - m_Keys[0].time;
        m_Keys[0].inTangent = 0.0f;
        m_Keys[0].outTangent = (dt > 0.0f) ? (m_Keys[1].value - m_Keys[0].value) / dt : 0.0f;
    }
    else if (idx == n - 1)
    {
        float dt = m_Keys[idx].time - m_Keys[idx - 1].time;
        m_Keys[idx].inTangent = (dt > 0.0f) ? (m_Keys[idx].value - m_Keys[idx - 1].value) / dt : 0.0f;
        m_Keys[idx].outTangent = 0.0f;
    }
    else
    {
        float dt1 = m_Keys[idx].time - m_Keys[idx - 1].time;
        float dt2 = m_Keys[idx + 1].time - m_Keys[idx].time;

        if (dt1 > 0.0f && dt2 > 0.0f)
        {
            float slope1 = (m_Keys[idx].value - m_Keys[idx - 1].value) / dt1;
            float slope2 = (m_Keys[idx + 1].value - m_Keys[idx].value) / dt2;
            float tangent = (slope1 * dt2 + slope2 * dt1) / (dt1 + dt2);
            m_Keys[idx].inTangent = tangent;
            m_Keys[idx].outTangent = tangent;
        }
        else
        {
            m_Keys[idx].inTangent = 0.0f;
            m_Keys[idx].outTangent = 0.0f;
        }
    }
}

void AnimationCurve::AutoSetTangents()
{
    for (size_t i = 0; i < m_Keys.size(); ++i)
        AutoSetTangent(i);
}

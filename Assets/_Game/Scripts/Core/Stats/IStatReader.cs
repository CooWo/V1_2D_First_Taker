using System;

namespace ShadowRPG.Core.Stats
{
    /// <summary>능력치 읽기 전용 창구 (ISP: 쓰기가 필요 없는 소비자는 이것만 봅니다).</summary>
    public interface IStatReader
    {
        float Get(StatType stat);
        event Action Changed;
    }

    /// <summary>
    /// 코어 스탯 → 파생 능력치 변환 규칙. 새 규칙은 클래스를 추가해 등록합니다 (OCP).
    /// </summary>
    public interface IStatDerivationRule
    {
        /// <summary>target 능력치에 더해질 값. raw는 파생 전 원시 값 조회 함수입니다.</summary>
        float Contribute(StatType target, Func<StatType, float> raw);
    }
}

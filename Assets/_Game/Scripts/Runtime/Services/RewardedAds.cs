using System;

namespace ShadowRPG.Runtime.Services
{
    /// <summary>보상형 광고. 광고 SDK(LevelPlay 등) 연동 전에는 즉시 보상을 주는 임시 구현을 씁니다.</summary>
    public interface IRewardedAdService
    {
        void Show(string placement, Action<bool> completed);
    }

    public sealed class InstantRewardedAds : IRewardedAdService
    {
        public void Show(string placement, Action<bool> completed) => completed(true);
    }

    public static class RewardedAds
    {
        public static IRewardedAdService Current { get; set; } = new InstantRewardedAds();
    }
}

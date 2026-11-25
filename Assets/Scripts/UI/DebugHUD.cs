using UnityEngine;

namespace LootHunters
{
    /// <summary>
    /// (폐기됨) 1단계 임시 화면이었습니다. 지금은 화면을 그리지 않습니다.
    /// 씬에 남아 있으면 정식 화면(GameUI)을 대신 붙이고 자기 자신은 지웁니다.
    /// 그래서 예전 씬에 DebugHUD가 여러 개 붙어 있어도 화면이 겹치지 않습니다.
    /// Game 오브젝트에서 이 컴포넌트를 직접 Remove Component 해도 됩니다.
    /// </summary>
    [AddComponentMenu("")]   // 컴포넌트 추가 메뉴에서 숨김
    public class DebugHUD : MonoBehaviour
    {
        void Awake()
        {
            if (GetComponent<GameUI>() == null && GetComponent<DungeonRun>() != null)
                gameObject.AddComponent<GameUI>();
            Destroy(this);
        }
    }
}

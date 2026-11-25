using System.Collections.Generic;

namespace ShadowRPG.Core
{
    /// <summary>
    /// 전투 질의용 임시 리스트 풀. 스킬 콜백 안에서 다시 질의해도 안전하도록 대여/반납 방식입니다.
    /// </summary>
    public static class ListPool<T>
    {
        static readonly Stack<List<T>> Pool = new Stack<List<T>>();

        public static List<T> Get() => Pool.Count > 0 ? Pool.Pop() : new List<T>(16);

        public static void Release(List<T> list)
        {
            list.Clear();
            Pool.Push(list);
        }
    }
}

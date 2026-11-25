using System;
using System.IO;
using ShadowRPG.Core.Meta;
using UnityEngine;

namespace ShadowRPG.Runtime
{
    /// <summary>기기 로컬 JSON 저장 (Application.persistentDataPath). 임시 파일에 쓴 뒤 교체해 손상을 막습니다.</summary>
    public sealed class FileProfileStore : IProfileStore
    {
        readonly string _path;

        public FileProfileStore(string fileName = "shadow_profile.json")
        {
            _path = Path.Combine(Application.persistentDataPath, fileName);
        }

        public string Path_ => _path;

        public GameProfile Load()
        {
            try
            {
                if (File.Exists(_path)) return ProfileSerializer.FromJson(File.ReadAllText(_path));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ShadowRPG] 저장 파일을 읽지 못해 새로 시작합니다: {e.Message}");
            }
            return new GameProfile();
        }

        public void Save(GameProfile profile)
        {
            string temp = _path + ".tmp";
            File.WriteAllText(temp, ProfileSerializer.ToJson(profile));
            if (File.Exists(_path)) File.Delete(_path);
            File.Move(temp, _path);
        }

        public void Delete()
        {
            if (File.Exists(_path)) File.Delete(_path);
        }
    }
}

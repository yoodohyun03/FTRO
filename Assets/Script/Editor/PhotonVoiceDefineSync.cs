#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Photon Voice 2 임포트 후 PunVoiceClient 타입이 로드되면 Scripting Define Symbols에 USE_PHOTON_VOICE를 넣고,
/// 패키지를 제거하면 심볼을 제거합니다.
/// </summary>
[InitializeOnLoad]
static class PhotonVoiceDefineSync
{
    const string Define = "USE_PHOTON_VOICE";

    static PhotonVoiceDefineSync()
    {
        EditorApplication.delayCall += SyncDefine;
    }

    static bool PunVoiceClientTypeExists()
    {
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                if (asm.GetType("Photon.Voice.PUN.PunVoiceClient") != null)
                    return true;
            }
            catch
            {
                // ignored
            }
        }

        return false;
    }

    static void SyncDefine()
    {
        var group = EditorUserBuildSettings.selectedBuildTargetGroup;
        string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
        var parts = defines.Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries).ToList();
        bool voicePresent = PunVoiceClientTypeExists();
        bool hasDefine = parts.Contains(Define);

        if (voicePresent && !hasDefine)
        {
            parts.Add(Define);
            PlayerSettings.SetScriptingDefineSymbolsForGroup(group, string.Join(";", parts));
            Debug.Log("[PhotonVoiceDefineSync] USE_PHOTON_VOICE 심볼을 추가했습니다. Photon Voice 2가 설치된 상태입니다.");
        }
        else if (!voicePresent && hasDefine)
        {
            parts.Remove(Define);
            PlayerSettings.SetScriptingDefineSymbolsForGroup(group, string.Join(";", parts));
            Debug.Log("[PhotonVoiceDefineSync] Photon Voice 어셈블리가 없어 USE_PHOTON_VOICE 심볼을 제거했습니다.");
        }
    }
}
#endif

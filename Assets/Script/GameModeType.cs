using Photon.Realtime;

/// <summary>방 생성 시 선택하는 게임 모드 (룸 프로퍼티 GameMode에 저장)</summary>
public enum GameModeType
{
    Normal = 0,
    BombPass = 1,
    SeekerMultiply = 2,
}

public static class GameModeTypeHelper
{
    public const string RoomPropertyKey = "GameMode";

    public static readonly string[] DisplayNames =
    {
        "일반모드",
        "폭탄 돌리기 모드",
        "술래 증식 모드",
    };

    public static int Count => DisplayNames.Length;

    public static string ToStorageId(GameModeType mode) => ((int)mode).ToString();

    public static GameModeType FromIndex(int index)
    {
        if (index < 0 || index >= Count) return GameModeType.Normal;
        return (GameModeType)index;
    }

    public static GameModeType FromRoom(Room room)
    {
        if (room == null || !room.CustomProperties.TryGetValue(RoomPropertyKey, out object raw))
            return GameModeType.Normal;

        if (raw is int i) return FromIndex(i);
        if (raw is byte b) return FromIndex(b);
        if (int.TryParse(raw.ToString(), out int parsed)) return FromIndex(parsed);
        return GameModeType.Normal;
    }

    public static string GetDisplayName(GameModeType mode) => DisplayNames[(int)mode];

    public static string GetDisplayName(int index) => GetDisplayName(FromIndex(index));
}

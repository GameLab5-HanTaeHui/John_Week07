using System.Collections.Generic;

/// <summary>
/// 역할 배정(순열)과 구역 배치를 하나의 시드 정수로 인코딩/디코딩하는 유틸리티입니다.
///
/// 인코딩 구조:
///   seed = LehmerEncode(rolePermutation) * zoneCount^characterCount + EncodeZones(zones)
///
/// 예) 캐릭터 7명, 역할 7개, 구역 4개:
///   역할 순열: 7! = 5040 가지
///   구역 배치: 4^7 = 16384 가지
///   최대 시드: 5040 × 16384 − 1 = 82,575,359 (int 범위 내)
/// </summary>
public static class SeedEncoder
{
    /// <summary>
    /// 역할 순열과 구역 배열을 하나의 시드로 인코딩합니다.
    /// </summary>
    /// <param name="rolePermutation">캐릭터 i에게 배정된 역할 인덱스 배열 (StageRoleConfig.Roles 기준)</param>
    /// <param name="zones">캐릭터 i의 초기 구역 ID 배열</param>
    /// <param name="roleCount">역할 총 수 (= 캐릭터 수)</param>
    /// <param name="zoneCount">구역 총 수</param>
    public static int Encode(int[] rolePermutation, int[] zones, int roleCount, int zoneCount)
    {
        int roleIndex  = LehmerEncode(rolePermutation, roleCount);
        int zoneValue  = EncodeZones(zones, zoneCount);
        int zoneBase   = IntPow(zoneCount, zones.Length);
        return roleIndex * zoneBase + zoneValue;
    }

    /// <summary>
    /// 시드를 역할 순열과 구역 배열로 디코딩합니다.
    /// </summary>
    public static void Decode(int seed, int characterCount, int roleCount, int zoneCount,
                              out int[] rolePermutation, out int[] zones)
    {
        int zoneBase      = IntPow(zoneCount, characterCount);
        int roleIndex     = seed / zoneBase;
        int zoneValue     = seed % zoneBase;
        rolePermutation   = LehmerDecode(roleIndex, roleCount);
        zones             = DecodeZones(zoneValue, characterCount, zoneCount);
    }

    /// <summary>
    /// 가능한 시드의 최댓값을 반환합니다. (Random.Range 상한값으로 사용)
    /// </summary>
    public static int GetMaxSeed(int characterCount, int roleCount, int zoneCount)
        => Factorial(roleCount) * IntPow(zoneCount, characterCount);

    // ── Lehmer 코드 ──────────────────────────────────────────────────────────

    /// <summary>
    /// 순열을 Lehmer 코드로 인코딩합니다.
    /// perm은 0~(n-1)의 중복 없는 순열이어야 합니다.
    /// </summary>
    private static int LehmerEncode(int[] perm, int n)
    {
        int index = 0;
        for (int i = 0; i < n; i++)
        {
            int smaller = 0;
            for (int j = i + 1; j < n; j++)
                if (perm[j] < perm[i]) smaller++;
            index += smaller * Factorial(n - 1 - i);
        }
        return index;
    }

    /// <summary>
    /// Lehmer 인덱스를 순열로 디코딩합니다.
    /// </summary>
    private static int[] LehmerDecode(int index, int n)
    {
        var available = new List<int>(n);
        for (int i = 0; i < n; i++) available.Add(i);

        var perm = new int[n];
        for (int i = n - 1; i >= 0; i--)
        {
            int f   = Factorial(i);
            int pos = index / f;
            index  %= f;
            perm[n - 1 - i] = available[pos];
            available.RemoveAt(pos);
        }
        return perm;
    }

    // ── 구역 인코딩 ──────────────────────────────────────────────────────────

    private static int EncodeZones(int[] zones, int zoneCount)
    {
        int result = 0;
        int mult   = 1;
        for (int i = 0; i < zones.Length; i++)
        {
            result += zones[i] * mult;
            mult   *= zoneCount;
        }
        return result;
    }

    private static int[] DecodeZones(int value, int count, int zoneCount)
    {
        var zones = new int[count];
        for (int i = 0; i < count; i++)
        {
            zones[i] = value % zoneCount;
            value    /= zoneCount;
        }
        return zones;
    }

    // ── 수학 유틸 ────────────────────────────────────────────────────────────

    private static int Factorial(int n)
    {
        int result = 1;
        for (int i = 2; i <= n; i++) result *= i;
        return result;
    }

    private static int IntPow(int b, int exp)
    {
        int result = 1;
        for (int i = 0; i < exp; i++) result *= b;
        return result;
    }
}

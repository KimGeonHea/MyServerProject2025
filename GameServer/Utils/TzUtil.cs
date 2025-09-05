using Microsoft.EntityFrameworkCore.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameServer.Utils
{
  /// <summary>
  /// 시간대 유틸: IANA/Windows 혼용 호환, 일일/주간 윈도우 계산, UTC 강제.
  /// DB에는 항상 UTC만 저장하고, 경계 계산만 로컬(TZ)로 한 뒤 UTC로 비교하세요.
  /// </summary>
  public static class TzUtil
  {
    // ---- IANA <-> Windows 매핑(자주 쓰는 것만) ----
    static readonly Dictionary<string, string> _ianaToWindows = new(StringComparer.OrdinalIgnoreCase)
    {
      ["Asia/Seoul"] = "Korea Standard Time",
      ["Asia/Tokyo"] = "Tokyo Standard Time",
      ["Asia/Shanghai"] = "China Standard Time",
      ["Asia/Taipei"] = "Taipei Standard Time",
      ["Asia/Hong_Kong"] = "China Standard Time",
      ["Asia/Singapore"] = "Singapore Standard Time",
      ["America/Los_Angeles"] = "Pacific Standard Time",
      ["America/New_York"] = "Eastern Standard Time",
      ["Europe/London"] = "GMT Standard Time",
      ["Europe/Berlin"] = "W. Europe Standard Time",
      ["Europe/Paris"] = "Romance Standard Time",
      ["UTC"] = "UTC"
    };

    static readonly Dictionary<string, string> _windowsToIana = new(StringComparer.OrdinalIgnoreCase)
    {
      ["Korea Standard Time"] = "Asia/Seoul",
      ["Tokyo Standard Time"] = "Asia/Tokyo",
      ["China Standard Time"] = "Asia/Shanghai",
      ["Taipei Standard Time"] = "Asia/Taipei",
      ["Singapore Standard Time"] = "Asia/Singapore",
      ["Pacific Standard Time"] = "America/Los_Angeles",
      ["Eastern Standard Time"] = "America/New_York",
      ["GMT Standard Time"] = "Europe/London",
      ["W. Europe Standard Time"] = "Europe/Berlin",
      ["Romance Standard Time"] = "Europe/Paris",
      ["UTC"] = "UTC"
    };

    /// <summary>
    /// 국가코드→기본 타임존(IANA) 추천(초기값). 필요 시 확장.
    /// </summary>
    static readonly Dictionary<string, string> _countryDefaultIana = new(StringComparer.OrdinalIgnoreCase)
    {
      ["KR"] = "Asia/Seoul",
      ["JP"] = "Asia/Tokyo",
      ["CN"] = "Asia/Shanghai",
      ["TW"] = "Asia/Taipei",
      ["HK"] = "Asia/Hong_Kong",
      ["SG"] = "Asia/Singapore",
      ["US"] = "America/Los_Angeles", // 다TZ 국가는 임시 기본
      ["GB"] = "Europe/London",
      ["DE"] = "Europe/Berlin",
      ["FR"] = "Europe/Paris"
    };

    /// <summary>
    /// 현재 OS에서 사용 가능한 TimeZoneInfo를 얻는다. (IANA/Windows ID 모두 허용)
    /// 실패 시 UTC.
    /// </summary>
    public static TimeZoneInfo Resolve(string tzId)
    {
      if (string.IsNullOrWhiteSpace(tzId))
        return TimeZoneInfo.Utc;

      // 1) 전달된 ID 그대로 시도
      if (TryFind(tzId, out var tz))
        return tz;

      // 2) IANA -> Windows
      if (_ianaToWindows.TryGetValue(tzId, out var winId) && TryFind(winId, out tz))
        return tz;

      // 3) Windows -> IANA
      if (_windowsToIana.TryGetValue(tzId, out var ianaId) && TryFind(ianaId, out tz))
        return tz;

      // 4) UTC 폴백
      return TimeZoneInfo.Utc;

      static bool TryFind(string id, out TimeZoneInfo info)
      {
        try 
        { 
          info = TimeZoneInfo.FindSystemTimeZoneById(id); return true; 
        }
        catch 
        { 
          info = null; return false; 
        }
      }
    }

    /// <summary>
    /// 국가코드로 초깃값 타임존(IANA) 추정. 미지정/미지원이면 "UTC".
    /// </summary>
    public static string DefaultIanaFromCountry(string country)
    {
      if (string.IsNullOrWhiteSpace(country)) return "UTC";
      return _countryDefaultIana.TryGetValue(country, out var iana) ? iana : "UTC";
    }

    /// <summary>
    /// 로컬(TZ) 기준 'resetHourLocal'~다음날 같은 시각까지의 일일창을 UTC로 반환.
    /// DST 전환일도 안전(로컬 시각을 Unspecified로 만들어 변환).
    /// </summary>
    public static (DateTime startUtc, DateTime endUtc) GetDailyWindowUtc(string tzId, DateTime nowUtc, byte resetHourLocal =9)
    {
      var tz = Resolve(tzId);
      nowUtc = AsUtc(nowUtc);

      var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);

      // 오늘의 리셋 시각(로컬)
      var startLocal = new DateTime(nowLocal.Year, nowLocal.Month, nowLocal.Day, resetHourLocal, 0, 0, DateTimeKind.Unspecified);
      if (nowLocal < startLocal)
        startLocal = startLocal.AddDays(-1); // 리셋 이전이면 전날이 오늘분

      var endLocal = startLocal.AddDays(1);

      return (ToUtcUnspecified(startLocal, tz), ToUtcUnspecified(endLocal, tz));
    }

    /// <summary>
    /// 주 시작(유저 요일 기준)을 UTC로 반환. anchorUtc는 보통 '오늘 일일창 시작'이 적절.
    /// </summary>
    public static DateTime GetWeekStartUtc(string tzId, DayOfWeek weekStart, DateTime anchorUtc, byte resetHourLocal = 9)
    {
      var (dayStartUtc, _) = GetDailyWindowUtc(tzId, anchorUtc, resetHourLocal);
      var tz = Resolve(tzId);
      var dayStartLocal = TimeZoneInfo.ConvertTimeFromUtc(dayStartUtc, tz);

      int diff = (7 + (int)dayStartLocal.DayOfWeek - (int)weekStart) % 7;
      var weekStartLocal = dayStartLocal.AddDays(-diff);

      return ToUtcUnspecified(weekStartLocal, tz);
    }

    /// <summary>
    /// DB 저장/비교 전 UTC로 강제.
    /// </summary>
    public static DateTime AsUtc(DateTime t)
    {
      if (t == default) return default;
      return t.Kind switch
      {
        DateTimeKind.Utc => t,
        DateTimeKind.Local => t.ToUniversalTime(),
        _ => DateTime.SpecifyKind(t, DateTimeKind.Utc) // Unspecified는 "이미 UTC"로 간주
      };
    }

    // --- 내부 유틸 ---

    static DateTime ToUtcUnspecified(DateTime localUnspecified, TimeZoneInfo tz)
    {
      // DST 겹침/누락 케이스도 TimeZoneInfo가 처리.
      // (겹침 시 표준/서머 중 하나로 해석되지만 게임 로직엔 충분)
      var unspecified = DateTime.SpecifyKind(localUnspecified, DateTimeKind.Unspecified);
      return TimeZoneInfo.ConvertTimeToUtc(unspecified, tz);
    }

  }
}

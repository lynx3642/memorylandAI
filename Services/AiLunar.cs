namespace SmartCem.Services;

/// <summary>Chuyển đổi Dương lịch ⇄ Âm lịch Việt Nam (thuật toán Hồ Ngọc Đức, múi giờ +7).</summary>
public static class Lunar
{
    const double TZ = 7.0;
    static readonly string[] Can = ["Giáp", "Ất", "Bính", "Đinh", "Mậu", "Kỷ", "Canh", "Tân", "Nhâm", "Quý"];
    static readonly string[] Chi = ["Tý", "Sửu", "Dần", "Mão", "Thìn", "Tỵ", "Ngọ", "Mùi", "Thân", "Dậu", "Tuất", "Hợi"];

    public record LunarDate(int Day, int Month, int Year, bool Leap)
    {
        public string YearName => $"{Can[(Year + 6) % 10]} {Chi[(Year + 8) % 12]}";
        public override string ToString() => $"{Day:D2}/{Month:D2}{(Leap ? " (nhuận)" : "")}/{Year}";
    }

    static int JdFromDate(int dd, int mm, int yy)
    {
        int a = (14 - mm) / 12, y = yy + 4800 - a, m = mm + 12 * a - 3;
        return dd + (153 * m + 2) / 5 + 365 * y + y / 4 - y / 100 + y / 400 - 32045;
    }

    static DateTime JdToDate(int jd)
    {
        int a = jd + 32044, b = (4 * a + 3) / 146097, c = a - b * 146097 / 4;
        int d = (4 * c + 3) / 1461, e = c - 1461 * d / 4, m = (5 * e + 2) / 153;
        return new DateTime(b * 100 + d - 4800 + m / 10, m + 3 - 12 * (m / 10), e - (153 * m + 2) / 5 + 1);
    }

    static int NewMoon(int k)
    {
        double T = k / 1236.85, T2 = T * T, T3 = T2 * T, dr = Math.PI / 180;
        double jd1 = 2415020.75933 + 29.53058868 * k + 0.0001178 * T2 - 0.000000155 * T3;
        jd1 += 0.00033 * Math.Sin((166.56 + 132.87 * T - 0.009173 * T2) * dr);
        double M = 359.2242 + 29.10535608 * k - 0.0000333 * T2 - 0.00000347 * T3;
        double Mpr = 306.0253 + 385.81691806 * k + 0.0107306 * T2 + 0.00001236 * T3;
        double F = 21.2964 + 390.67050646 * k - 0.0016528 * T2 - 0.00000239 * T3;
        double c1 = (0.1734 - 0.000393 * T) * Math.Sin(M * dr) + 0.0021 * Math.Sin(2 * dr * M);
        c1 -= 0.4068 * Math.Sin(Mpr * dr) - 0.0161 * Math.Sin(dr * 2 * Mpr);
        c1 -= 0.0004 * Math.Sin(dr * 3 * Mpr);
        c1 += 0.0104 * Math.Sin(dr * 2 * F) - 0.0051 * Math.Sin(dr * (M + Mpr));
        c1 -= 0.0074 * Math.Sin(dr * (M - Mpr)) - 0.0004 * Math.Sin(dr * (2 * F + M));
        c1 -= 0.0004 * Math.Sin(dr * (2 * F - M)) + 0.0006 * Math.Sin(dr * (2 * F + Mpr));
        c1 += 0.0010 * Math.Sin(dr * (2 * F - Mpr)) + 0.0005 * Math.Sin(dr * (2 * Mpr + M));
        double dt = T < -11
            ? 0.001 + 0.000839 * T + 0.0002261 * T2 - 0.00000845 * T3 - 0.000000081 * T * T3
            : -0.000278 + 0.000265 * T + 0.000262 * T2;
        return (int)Math.Floor(jd1 + c1 - dt + 0.5 + TZ / 24);
    }

    static int SunLongitude(int jdn)
    {
        double T = (jdn - 2451545.5 - TZ / 24) / 36525, T2 = T * T, dr = Math.PI / 180;
        double M = 357.52910 + 35999.05030 * T - 0.0001559 * T2 - 0.00000048 * T * T2;
        double L0 = 280.46645 + 36000.76983 * T + 0.0003032 * T2;
        double DL = (1.914600 - 0.004817 * T - 0.000014 * T2) * Math.Sin(dr * M);
        DL += (0.019993 - 0.000101 * T) * Math.Sin(dr * 2 * M) + 0.000290 * Math.Sin(dr * 3 * M);
        double L = (L0 + DL) * dr;
        L -= Math.PI * 2 * Math.Floor(L / (Math.PI * 2));
        return (int)Math.Floor(L / Math.PI * 6);
    }

    static int LunarMonth11(int yy)
    {
        int off = JdFromDate(31, 12, yy) - 2415021;
        int k = (int)Math.Floor(off / 29.530588853);
        int nm = NewMoon(k);
        return SunLongitude(nm) >= 9 ? NewMoon(k - 1) : nm;
    }

    static int LeapMonthOffset(int a11)
    {
        int k = (int)Math.Floor((a11 - 2415021.076998695) / 29.530588853 + 0.5);
        int last, i = 1, arc = SunLongitude(NewMoon(k + i));
        do { last = arc; i++; arc = SunLongitude(NewMoon(k + i)); } while (arc != last && i < 14);
        return i - 1;
    }

    public static LunarDate FromSolar(DateTime d)
    {
        int dd = d.Day, mm = d.Month, yy = d.Year;
        int day = JdFromDate(dd, mm, yy);
        int k = (int)Math.Floor((day - 2415021.076998695) / 29.530588853);
        int monthStart = NewMoon(k + 1);
        if (monthStart > day) monthStart = NewMoon(k);
        int a11 = LunarMonth11(yy), b11 = a11, ly;
        if (a11 >= monthStart) { ly = yy; a11 = LunarMonth11(yy - 1); }
        else { ly = yy + 1; b11 = LunarMonth11(yy + 1); }
        int lday = day - monthStart + 1, diff = (monthStart - a11) / 29, lmonth = diff + 11;
        bool leap = false;
        if (b11 - a11 > 365)
        {
            int leapDiff = LeapMonthOffset(a11);
            if (diff >= leapDiff) { lmonth = diff + 10; if (diff == leapDiff) leap = true; }
        }
        if (lmonth > 12) lmonth -= 12;
        if (lmonth >= 11 && diff < 4) ly -= 1;
        return new LunarDate(lday, lmonth, ly, leap);
    }

    public static DateTime? ToSolar(int ld, int lm, int ly, bool leap = false)
    {
        int a11, b11;
        if (lm < 11) { a11 = LunarMonth11(ly - 1); b11 = LunarMonth11(ly); }
        else { a11 = LunarMonth11(ly); b11 = LunarMonth11(ly + 1); }
        int k = (int)Math.Floor(0.5 + (a11 - 2415021.076998695) / 29.530588853);
        int off = lm - 11; if (off < 0) off += 12;
        if (b11 - a11 > 365)
        {
            int leapOff = LeapMonthOffset(a11), leapMonth = leapOff - 2;
            if (leapMonth < 0) leapMonth += 12;
            if (leap && lm != leapMonth) return null;
            if (leap || off >= leapOff) off++;
        }
        return JdToDate(NewMoon(k + off) + ld - 1);
    }

    /// <summary>Ngày dương lịch gần nhất (>= from) ứng với ngày/tháng âm lịch (giỗ hằng năm).</summary>
    public static DateTime? NextSolarOf(int ld, int lm, DateTime from)
    {
        for (int y = from.Year - 1; y <= from.Year + 1; y++)
        {
            for (int day = ld; day >= Math.Max(1, ld - 1); day--)
            {
                var s = ToSolar(day, lm, y);
                if (s is null) continue;
                var back = FromSolar(s.Value);
                if (back.Day != day || back.Month != lm) continue;
                if (s.Value.Date >= from.Date) return s.Value.Date;
                break;
            }
        }
        return null;
    }

    public static (int d, int m)? ParseDayMonth(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var m = System.Text.RegularExpressions.Regex.Match(text, @"(\d{1,2})\s*/\s*(\d{1,2})");
        return m.Success ? (int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value)) : null;
    }
}

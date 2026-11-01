namespace SmartCem.Services;

// ═══════════════════════════════════════════════════════════════════════
//  MemoryLand AI – Bộ lõi AI mô phỏng (chạy cục bộ, không gọi dịch vụ trả phí)
//  Các thuật toán là thật (D8 flow, đóng gói không gian, hồi quy theo mùa,
//  Hungarian) – chỉ riêng dữ liệu ảnh Drone là được sinh mô phỏng.
// ═══════════════════════════════════════════════════════════════════════

public record SiteGrave(int Id, double X, double Y, double W, double D, double Angle, string Type, double Conf, double Weed);
public record SiteTree(double X, double Y, double R);
public record Site(double W, double H, double Cell, int Cols, int Rows, List<SiteGrave> Graves, List<SiteTree> Trees, double[] Dem);

public static class Geo
{
    // Gốc toạ độ khu nghĩa trang mô phỏng (Ba Vì, Hà Nội) – bản đồ 400m × 250m
    public const double Lat0 = 21.0300, Lng0 = 105.8500;
    public static (double lat, double lng) LotGps(double xPct, double yPct)
        => (Lat0 - yPct * 2.5 * 8.98e-6, Lng0 + xPct * 4.0 * 9.62e-6);

    public static double Haversine(double lat1, double lng1, double lat2, double lng2)
    {
        const double R = 6371000;
        double dLat = (lat2 - lat1) * Math.PI / 180, dLng = (lng2 - lng1) * Math.PI / 180;
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return 2 * R * Math.Asin(Math.Sqrt(a));
    }
}

// ─── Khu nghĩa trang tự phát (dữ liệu "ảnh Drone" mô phỏng) ──────────────
public static class SiteSim
{
    public static Site Generate(int seed = 7, double W = 120, double H = 80)
    {
        var rng = new Random(seed);
        const double cell = 2.0;
        int cols = (int)(W / cell), rows = (int)(H / cell);

        // Cây xanh
        var trees = new List<SiteTree>();
        for (int i = 0, guard = 0; i < 16 && guard < 500; guard++)
        {
            var t = new SiteTree(6 + rng.NextDouble() * (W - 12), 6 + rng.NextDouble() * (H - 12), 1.1 + rng.NextDouble() * 2.0);
            if (trees.Any(o => Dist(o.X, o.Y, t.X, t.Y) < o.R + t.R + 4)) continue;
            trees.Add(t); i++;
        }

        // Mộ theo cụm dòng họ – mỗi cụm một hướng riêng ("mạnh ai nấy chôn")
        var graves = new List<SiteGrave>();
        var clusters = Enumerable.Range(0, 13).Select(_ => (cx: 8 + rng.NextDouble() * (W - 16), cy: 8 + rng.NextDouble() * (H - 16), ang: rng.NextDouble() * 180 - 90)).ToList();
        int id = 1;
        for (int guard = 0; graves.Count < 150 && guard < 20000; guard++)
        {
            var c = clusters[rng.Next(clusters.Count)];
            double x = c.cx + Gauss(rng) * 7, y = c.cy + Gauss(rng) * 7;
            if (x < 3 || y < 3 || x > W - 3 || y > H - 3) continue;
            double roll = rng.NextDouble();
            string type; double w, d;
            if (roll < 0.70) { type = "Mộ đơn"; w = 1.0; d = 2.0; }
            else if (roll < 0.88) { type = "Mộ đôi"; w = 1.8; d = 2.0; }
            else if (roll < 0.95) { type = "Cải táng"; w = 0.8; d = 1.2; }
            else { type = "Mộ gia tộc"; w = 3.4; d = 3.4; }
            double r = Math.Max(w, d) * 0.5 + 0.35;
            if (graves.Any(o => Dist(o.X, o.Y, x, y) < r + Math.Max(o.W, o.D) * 0.5 + 0.35)) continue;
            if (trees.Any(t => Dist(t.X, t.Y, x, y) < t.R + r)) continue;
            double near = trees.Count == 0 ? 99 : trees.Min(t => Dist(t.X, t.Y, x, y) - t.R);
            double conf = Math.Clamp(0.975 - rng.NextDouble() * 0.06 - (near < 4 ? (4 - near) * 0.06 : 0), 0.62, 0.99);
            graves.Add(new SiteGrave(id++, x, y, w, d, c.ang + Gauss(rng) * 9, type, Math.Round(conf, 3), Math.Round(Math.Pow(rng.NextDouble(), 1.6), 2)));
        }

        // DEM: dốc nhẹ về phía nam + gò + trũng ở góc đông-nam
        var dem = new double[cols * rows];
        for (int j = 0; j < rows; j++)
            for (int i = 0; i < cols; i++)
            {
                double x = (i + 0.5) * cell, y = (j + 0.5) * cell;
                double z = 21.0 - 0.035 * y + 0.012 * x
                    + 2.4 * Math.Exp(-(Math.Pow(x - 28, 2) + Math.Pow(y - 18, 2)) / 320)
                    - 2.0 * Math.Exp(-(Math.Pow(x - 92, 2) + Math.Pow(y - 58, 2)) / 190)
                    + 0.25 * Math.Sin(x / 8.0) * Math.Cos(y / 6.5);
                dem[j * cols + i] = Math.Round(z, 3);
            }
        return new Site(W, H, cell, cols, rows, graves, trees, dem);
    }

    static double Gauss(Random r) => Math.Sqrt(-2 * Math.Log(1 - r.NextDouble())) * Math.Cos(2 * Math.PI * r.NextDouble());
    public static double Dist(double x1, double y1, double x2, double y2) => Math.Sqrt((x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2));
}

// ─── AI Terrain: độ dốc, hướng dòng chảy D8, tích lũy dòng chảy ──────────
public record TerrainResult(double[] Slope, int[] Acc, List<int[]> Channels, List<int[]> Flood, double MaxSlope, double AvgSlope, int Pits);

public static class TerrainAi
{
    static readonly int[] DX = [1, 1, 0, -1, -1, -1, 0, 1], DY = [0, 1, 1, 1, 0, -1, -1, -1];

    public static TerrainResult Analyze(Site s)
    {
        int C = s.Cols, R = s.Rows, n = C * R;
        var slope = new double[n]; var dir = new int[n]; var acc = new int[n];
        int pits = 0;
        for (int j = 0; j < R; j++)
            for (int i = 0; i < C; i++)
            {
                int k = j * C + i;
                double zl = s.Dem[j * C + Math.Max(0, i - 1)], zr = s.Dem[j * C + Math.Min(C - 1, i + 1)];
                double zu = s.Dem[Math.Max(0, j - 1) * C + i], zd = s.Dem[Math.Min(R - 1, j + 1) * C + i];
                slope[k] = Math.Atan(Math.Sqrt(Math.Pow((zr - zl) / (2 * s.Cell), 2) + Math.Pow((zd - zu) / (2 * s.Cell), 2))) * 180 / Math.PI;
                double best = 0; int bd = -1;
                for (int d = 0; d < 8; d++)
                {
                    int ni = i + DX[d], nj = j + DY[d];
                    if (ni < 0 || nj < 0 || ni >= C || nj >= R) continue;
                    double drop = (s.Dem[k] - s.Dem[nj * C + ni]) / (d % 2 == 0 ? 1.0 : 1.4142);
                    if (drop > best) { best = drop; bd = d; }
                }
                dir[k] = bd;
                if (bd < 0) pits++;
            }
        // Tích lũy: duyệt từ cao xuống thấp
        var order = Enumerable.Range(0, n).OrderByDescending(k => s.Dem[k]).ToArray();
        foreach (var k in order) acc[k] += 1;
        foreach (var k in order)
        {
            if (dir[k] < 0) continue;
            int ni = k % C + DX[dir[k]], nj = k / C + DY[dir[k]];
            acc[nj * C + ni] += acc[k];
        }
        var channels = new List<int[]>(); var flood = new List<int[]>();
        var floodSet = new HashSet<int>();
        for (int k = 0; k < n; k++)
        {
            if (acc[k] >= 70) channels.Add([k % C, k / C, acc[k]]);
            if (acc[k] >= 40 && slope[k] < 2.0 && (dir[k] < 0 || acc[k] >= 120)) { flood.Add([k % C, k / C, acc[k]]); floodSet.Add(k); }
        }
        // Vùng trũng quanh điểm đọng nước
        for (int k = 0; k < n; k++)
            if (dir[k] < 0)
                for (int dj = -2; dj <= 2; dj++)
                    for (int di = -2; di <= 2; di++)
                    {
                        int ni = k % C + di, nj = k / C + dj;
                        if (ni < 0 || nj < 0 || ni >= C || nj >= R) continue;
                        if (floodSet.Add(nj * C + ni)) flood.Add([ni, nj, acc[nj * C + ni]]);
                    }
        return new TerrainResult(slope.Select(v => Math.Round(v, 2)).ToArray(), acc, channels, flood, slope.Max(), slope.Average(), pits);
    }
}

// ─── AI Planner: tối ưu phân lô 3 phương án ───────────────────────────────
public static class PlannerAi
{
    const double LotW = 1.3, LotD = 2.6;       // ô mộ (gồm lề) ≈ 3,4 m²

    record Rect(double X, double Y, double W, double H);

    public static object Run(Site s, TerrainResult t)
    {
        var chan = t.Channels.Select(c => ((c[0] + 0.5) * s.Cell, (c[1] + 0.5) * s.Cell)).ToList();
        int baseline = Baseline(s, chan);
        var plans = new[]
        {
            Build("A", "Tối đa ô mộ", "Tối đa hoá số ô sử dụng, hàng lối thẳng, trồng bù cây nhỏ", s, chan, 1.5, 1.0, 14, 0, 0, 0.3, true, false, 15, baseline),
            Build("B", "Cân bằng cây xanh", "Giữ toàn bộ cây lớn, dải xanh cảnh quan 3 m quanh khuôn viên + băng cây giữa các khối", s, chan, 3.0, 1.1, 12, 4, 1.5, 0.6, false, false, 10, baseline),
            Build("C", "Giảm thiểu di dời", "Giữ nguyên mộ hiện hữu nằm ngoài đường/rãnh, chỉ lấp ô trống còn lại", s, chan, 1.5, 0.9, 14, 0, 0, 0.4, false, true, 0, baseline),
        };
        return new { width = s.W, height = s.H, existing = s.Graves.Count, baseline, plans };
    }

    // Hiện trạng "mạnh ai nấy chôn": số ô tối đa khi chèn thêm ngẫu nhiên xen kẽ
    static int Baseline(Site s, List<(double, double)> chan)
    {
        var rng = new Random(99);
        var added = new List<(double, double)>();
        for (int a = 0; a < 30000; a++)
        {
            double x = 1.5 + rng.NextDouble() * (s.W - 3), y = 1.5 + rng.NextDouble() * (s.H - 3);
            if (s.Trees.Any(tr => SiteSim.Dist(tr.X, tr.Y, x, y) < tr.R + 1.0)) continue;
            if (chan.Any(c => SiteSim.Dist(c.Item1, c.Item2, x, y) < 1.5)) continue;
            if (s.Graves.Any(g => SiteSim.Dist(g.X, g.Y, x, y) < Math.Max(g.W, g.D) * 0.5 + 1.05)) continue;
            if (added.Any(p => SiteSim.Dist(p.Item1, p.Item2, x, y) < 1.95)) continue;
            added.Add((x, y));
        }
        return s.Graves.Count + added.Count;
    }

    static object Build(string code, string name, string desc, Site s, List<(double, double)> chan,
        double margin, double aisleY, int perBlock, int greenEvery, double greenH, double treeBuf,
        bool removeSmallTrees, bool keepExisting, double tol, int baseline)
    {
        double W = s.W, H = s.H;
        var roads = new List<Rect>(); var greens = new List<Rect>();
        double rx0 = W / 2 - 1.5, rx1 = W / 2 + 1.5;
        roads.Add(new Rect(rx0, margin, 3, H - margin));
        var keptTrees = s.Trees.Where(tr => !(removeSmallTrees && tr.R < 2.0)).ToList();
        int removedTrees = s.Trees.Count - keptTrees.Count;

        // Sinh lưới ô
        var cells = new List<(double x, double y)>();
        double y = margin; int pair = 0;
        while (y + 2 * LotD <= H - margin)
        {
            for (int r = 0; r < 2; r++)
            {
                double x = margin; int cnt = 0;
                while (x + LotW <= W - margin)
                {
                    if (x < rx1 && x + LotW > rx0) { x = rx1; continue; }
                    cells.Add((x, y + r * LotD)); x += LotW;
                    if (++cnt % perBlock == 0) x += 1.2;
                }
            }
            double bandY = y + 2 * LotD;
            if (bandY + aisleY < H - margin) roads.Add(new Rect(margin, bandY, W - 2 * margin, aisleY));
            y += 2 * LotD + aisleY; pair++;
            if (greenEvery > 0 && pair % greenEvery == 0) { greens.Add(new Rect(margin, y, W - 2 * margin, greenH)); y += greenH; }
        }

        bool Blocked(double cx, double cy, double rad)
        {
            foreach (var tr in keptTrees) if (SiteSim.Dist(tr.X, tr.Y, cx, cy) < tr.R + treeBuf + rad) return true;
            foreach (var c in chan) if (SiteSim.Dist(c.Item1, c.Item2, cx, cy) < 1.0 + rad) return true;
            return false;
        }

        var valid = cells.Where(c => !Blocked(c.x + LotW / 2, c.y + LotD / 2, 0.85)).ToList();
        var existing = s.Graves;
        bool GraveBlocked(SiteGrave g)
        {
            double rad = Math.Max(g.W, g.D) * (keepExisting ? 0.28 : 0.45);
            if (Blocked(g.X, g.Y, rad)) return true;
            if (g.X < margin + rad || g.Y < margin + rad || g.X > W - margin - rad || g.Y > H - margin - rad) return true;
            foreach (var r in roads.Concat(greens)) if (CircleHitsRect(g.X, g.Y, rad, r)) return true;
            return false;
        }

        List<(double x, double y)> freeCells; var kept = new List<double[]>(); var moved = new List<double[]>();
        int relocations; int capacity;
        if (keepExisting)
        {
            var retained = existing.Where(g => !GraveBlocked(g)).ToList();
            var open = valid.Where(c => !retained.Any(g => SiteSim.Dist(g.X, g.Y, c.x + LotW / 2, c.y + LotD / 2) < Math.Max(g.W, g.D) * 0.5 + 1.25)).ToList();
            relocations = existing.Count - retained.Count;
            foreach (var g in retained) kept.Add([Math.Round(g.X, 2), Math.Round(g.Y, 2), g.W, g.D, Math.Round(g.Angle, 1)]);
            var pool = open.ToList();
            foreach (var g in existing.Except(retained))
            {
                if (pool.Count == 0) break;
                var best = pool.OrderBy(c => SiteSim.Dist(c.x, c.y, g.X, g.Y)).First();
                pool.Remove(best); moved.Add([Math.Round(best.x, 2), Math.Round(best.y, 2)]);
            }
            freeCells = pool;
            capacity = retained.Count + open.Count;
        }
        else
        {
            var pool = valid.ToList();
            foreach (var g in existing)
            {
                if (pool.Count == 0) break;
                var best = pool.OrderBy(c => SiteSim.Dist(c.x + LotW / 2, c.y + LotD / 2, g.X, g.Y)).First();
                pool.Remove(best); moved.Add([Math.Round(best.x, 2), Math.Round(best.y, 2)]);
            }
            freeCells = pool;
            capacity = valid.Count;
            relocations = existing.Count(g => GraveBlocked(g) || Math.Abs(NormAng(g.Angle)) > tol);
        }

        double belt = W * H - (W - 2 * margin) * (H - 2 * margin);
        double greenArea = belt + greens.Sum(g => g.W * g.H) + keptTrees.Sum(tr => Math.PI * tr.R * tr.R);
        double roadLen = roads.Sum(r => Math.Max(r.W, r.H));
        int gain = capacity - baseline;
        double gainPct = baseline == 0 ? 0 : Math.Round(100.0 * gain / baseline, 1);
        double reloc = 100.0 * relocations / Math.Max(1, existing.Count);
        double greenPct = Math.Round(100 * greenArea / (W * H), 1);
        // Điểm tổng hợp 0-100 (trọng số: quỹ đất 45%, ít di dời 30%, cây xanh 25%)
        double score = Math.Clamp(45 * Math.Min(1, Math.Max(0, gainPct) / 35) + 30 * (1 - reloc / 100) + 25 * Math.Min(1, greenPct / 25), 0, 100);

        return new
        {
            code, name, description = desc,
            capacity, newSlots = Math.Max(0, capacity - existing.Count), gain, gainPct,
            relocations, relocationPct = Math.Round(reloc, 1), greenPct, roadLengthM = Math.Round(roadLen),
            treesKept = keptTrees.Count, treesRemoved = removedTrees, score = Math.Round(score, 1),
            cells = freeCells.Select(c => new[] { Math.Round(c.x, 2), Math.Round(c.y, 2) }).ToList(),
            moved, kept,
            roads = roads.Select(r => new[] { r.X, r.Y, r.W, r.H }).ToList(),
            greens = greens.Select(r => new[] { r.X, r.Y, r.W, r.H }).ToList(),
            belt = margin,
            trees = keptTrees.Select(tr => new[] { tr.X, tr.Y, tr.R }).ToList(),
            lot = new[] { LotW, LotD }
        };
    }

    static double NormAng(double a) { a %= 180; return a > 90 ? a - 180 : a < -90 ? a + 180 : a; }
    static bool CircleHitsRect(double cx, double cy, double r, Rect rc)
    {
        double nx = Math.Clamp(cx, rc.X, rc.X + rc.W), ny = Math.Clamp(cy, rc.Y, rc.Y + rc.H);
        return SiteSim.Dist(cx, cy, nx, ny) < r;
    }
}

// ─── AI Forecast: dự báo thời điểm hết đất ────────────────────────────────
public static class ForecastAi
{
    static readonly double[] SeasonTrue = [1.25, 1.20, 1.10, 0.95, 0.85, 0.80, 0.80, 0.82, 0.90, 1.00, 1.10, 1.25];

    public static object Run(string zone, int capacity, double fillRate, DateTime now)
    {
        var rng = new Random(1000 + zone[0]);
        double baseRate = capacity * 0.012;
        var start = new DateTime(now.Year, now.Month, 1).AddMonths(-36);
        var hist = new List<double>();
        for (int i = 0; i < 36; i++)
        {
            var m = start.AddMonths(i);
            double v = baseRate * (1 + 0.006 * i) * SeasonTrue[m.Month - 1] * (0.88 + rng.NextDouble() * 0.24);
            hist.Add(Math.Round(v, 2));
        }
        // 1) xu hướng: hồi quy tuyến tính trên trung bình trượt 12 tháng  2) chỉ số mùa
        var ma = new double[36];
        for (int i = 0; i < 36; i++)
        {
            int a = Math.Max(0, i - 6), b = Math.Min(35, i + 5);
            ma[i] = hist.Skip(a).Take(b - a + 1).Average();
        }
        double mx = 17.5, my = ma.Average(), sxy = 0, sxx = 0;
        for (int i = 0; i < 36; i++) { sxy += (i - mx) * (ma[i] - my); sxx += (i - mx) * (i - mx); }
        double slope = sxy / sxx, icpt = my - slope * mx;
        var seas = new double[12]; var cnt = new int[12];
        for (int i = 0; i < 36; i++) { int mo = start.AddMonths(i).Month - 1; seas[mo] += hist[i] / (icpt + slope * i); cnt[mo]++; }
        for (int i = 0; i < 12; i++) seas[i] /= Math.Max(1, cnt[i]);
        double sm = seas.Average(); for (int i = 0; i < 12; i++) seas[i] /= sm;

        double Fit(int i, DateTime d) => (icpt + slope * i) * seas[d.Month - 1];
        double mape = hist.Select((h, i) => Math.Abs(h - Fit(i, start.AddMonths(i))) / h).TakeLast(12).Average() * 100;
        double sigma = Math.Sqrt(hist.Select((h, i) => Math.Pow(h - Fit(i, start.AddMonths(i)), 2)).Average());

        int used = (int)Math.Round(capacity * fillRate), remaining = capacity - used;
        var fc = new List<double>(); var labels = new List<string>(); double cum = 0; DateTime? depletion = null;
        for (int i = 0; i < 120; i++)
        {
            var d = start.AddMonths(36 + i);
            double v = Math.Max(0, Fit(36 + i, d));
            cum += v;
            if (depletion is null && cum >= remaining) depletion = d;
            if (i < 24) { fc.Add(Math.Round(v, 2)); labels.Add($"{d.Month:D2}/{d.Year}"); }
        }
        int? months = depletion is null ? null : (depletion.Value.Year - now.Year) * 12 + depletion.Value.Month - now.Month;
        string risk = months is null ? "Thấp" : months < 18 ? "Cao" : months < 36 ? "Trung bình" : "Thấp";
        return new
        {
            zone, capacity, used, remaining,
            historyLabels = Enumerable.Range(0, 36).Select(i => { var d = start.AddMonths(i); return $"{d.Month:D2}/{d.Year}"; }).ToList(),
            history = hist, forecastLabels = labels, forecast = fc,
            band = Math.Round(1.28 * sigma, 2),
            depletion = depletion is null ? "> 10 năm" : $"{depletion.Value.Month:D2}/{depletion.Value.Year}",
            monthsLeft = months, risk,
            mape = Math.Round(mape, 1), model = "Holt-Winters rút gọn (xu hướng tuyến tính + chỉ số mùa)",
            avgPerMonth = Math.Round(hist.TakeLast(12).Average(), 2)
        };
    }
}

// ─── AI Dispatch: ghép cặp đơn ⇄ đối tác (thuật toán Hungarian) ───────────
public record DispatchPartner(int Id, double Lat, double Lng, double Rating, int Load, bool Available);
public record DispatchJob(int Id, double Lat, double Lng);
public record DispatchResult(int JobId, int? PartnerId, double DistanceKm, double EtaMin, double Cost);

public static class DispatchAi
{
    public static double Cost(DispatchPartner p, DispatchJob j, out double km)
    {
        km = Geo.Haversine(p.Lat, p.Lng, j.Lat, j.Lng) / 1000;
        return km * 1.0 + (5 - p.Rating) * 3.0 + p.Load * 1.5;
    }

    public static List<DispatchResult> Assign(List<DispatchJob> jobs, List<DispatchPartner> partners, ISet<int>? exclude = null)
    {
        var ps = partners.Where(p => p.Available && (exclude == null || !exclude.Contains(p.Id))).ToList();
        var res = new List<DispatchResult>();
        if (jobs.Count == 0) return res;
        int n = jobs.Count, m = Math.Max(ps.Count, n);
        var a = new double[n + 1, m + 1];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < m; j++)
                a[i + 1, j + 1] = j < ps.Count ? Cost(ps[j], jobs[i], out _) : 1e6;

        var u = new double[n + 1]; var v = new double[m + 1]; var p = new int[m + 1]; var way = new int[m + 1];
        for (int i = 1; i <= n; i++)
        {
            p[0] = i; int j0 = 0;
            var minv = Enumerable.Repeat(double.MaxValue, m + 1).ToArray(); var used = new bool[m + 1];
            do
            {
                used[j0] = true; int i0 = p[j0], j1 = 0; double delta = double.MaxValue;
                for (int j = 1; j <= m; j++)
                {
                    if (used[j]) continue;
                    double cur = a[i0, j] - u[i0] - v[j];
                    if (cur < minv[j]) { minv[j] = cur; way[j] = j0; }
                    if (minv[j] < delta) { delta = minv[j]; j1 = j; }
                }
                for (int j = 0; j <= m; j++)
                    if (used[j]) { u[p[j]] += delta; v[j] -= delta; } else minv[j] -= delta;
                j0 = j1;
            } while (p[j0] != 0);
            do { int j1 = way[j0]; p[j0] = p[j1]; j0 = j1; } while (j0 != 0);
        }
        var ans = new int[n];
        for (int j = 1; j <= m; j++) if (p[j] != 0) ans[p[j] - 1] = j - 1;
        for (int i = 0; i < n; i++)
        {
            if (ans[i] >= ps.Count) { res.Add(new DispatchResult(jobs[i].Id, null, 0, 0, 0)); continue; }
            var cost = Cost(ps[ans[i]], jobs[i], out var km);
            res.Add(new DispatchResult(jobs[i].Id, ps[ans[i]].Id, Math.Round(km, 2), Math.Round(km / 25.0 * 60 + 5, 0), Math.Round(cost, 2)));
        }
        return res;
    }
}

// ─── AI Verify: kiểm định nghiệm thu 3 lớp ────────────────────────────────
public record VerifyLayer(string Name, bool Pass, double Score, string Detail);

public static class VerifyAi
{
    public static int Hamming(string a, string b)
    {
        if (a.Length != b.Length || a.Length == 0) return 64;
        int d = 0;
        for (int i = 0; i < a.Length; i++)
            d += System.Numerics.BitOperations.PopCount((uint)(Convert.ToInt32(a[i].ToString(), 16) ^ Convert.ToInt32(b[i].ToString(), 16)));
        return d;
    }

    public static (bool pass, double score, List<VerifyLayer> layers) Run(
        double checkinDistM, double afterDistM, bool beforeInApp, bool afterInApp,
        DateTime beforeAt, DateTime afterAt, DateTime acceptedAt, DateTime now,
        string beforeHash, string afterHash, double weedBefore, double weedAfter)
    {
        // Lớp 1 – GPS + QR tại mộ
        double worst = Math.Max(checkinDistM, afterDistM);
        bool l1 = worst <= 5;
        var layer1 = new VerifyLayer("Lớp 1 · Định vị GPS & QR", l1, l1 ? Math.Round(1 - worst / 10, 2) : 0,
            l1 ? $"Check-in cách mộ {checkinDistM:0.0} m, ảnh nghiệm thu cách {afterDistM:0.0} m (≤ 5 m)" : $"Sai vị trí: lệch {worst:0.0} m so với tọa độ mộ (cho phép ≤ 5 m)");

        // Lớp 2 – Siêu dữ liệu ảnh (EXIF): chụp trực tiếp trong app, thời gian thực, đúng thứ tự
        var issues = new List<string>();
        if (!beforeInApp || !afterInApp) issues.Add("ảnh tải từ thư viện, không chụp trong app");
        if (beforeAt < acceptedAt.AddMinutes(-1) || afterAt < acceptedAt.AddMinutes(-1)) issues.Add("ảnh chụp trước thời điểm nhận việc (ảnh cũ)");
        if ((now - afterAt).TotalMinutes > 15) issues.Add("ảnh chụp quá 15 phút trước");
        if (afterAt <= beforeAt.AddMilliseconds(500)) issues.Add("ảnh sau không muộn hơn ảnh trước");
        bool l2 = issues.Count == 0;
        var layer2 = new VerifyLayer("Lớp 2 · EXIF & thời gian thực", l2, l2 ? 1 : 0,
            l2 ? $"Chụp trong app, đúng thứ tự, cách nhau {(afterAt - beforeAt).TotalSeconds:0} giây; hash EXIF hợp lệ" : string.Join("; ", issues));

        // Lớp 3 – AI hình ảnh (mô phỏng CLIP/Siamese): cùng một vị trí + sạch hơn
        int ham = Hamming(beforeHash, afterHash);
        bool samePlace = ham <= 28, changed = ham >= 1;
        double drop = weedBefore - weedAfter;
        bool cleaner = drop >= 0.15 || weedAfter <= 0.10;
        bool l3 = samePlace && changed && cleaner;
        double s3 = Math.Clamp((samePlace ? 0.5 : 0) + Math.Min(0.5, Math.Max(0, drop)), 0, 1);
        var layer3 = new VerifyLayer("Lớp 3 · AI đối chiếu ảnh trước/sau", l3, Math.Round(s3, 2),
            !samePlace ? $"Khác bối cảnh (Hamming {ham}/64 > 28) – nghi nhầm mộ"
            : !changed ? "Ảnh trước/sau giống hệt – không có dấu hiệu làm việc"
            : !cleaner ? $"Mức độ sạch cải thiện chưa đủ (cỏ/rác {weedBefore:P0} → {weedAfter:P0})"
            : $"Cùng vị trí (Hamming {ham}/64), cỏ/rác giảm {weedBefore:P0} → {weedAfter:P0}");

        var layers = new List<VerifyLayer> { layer1, layer2, layer3 };
        double total = Math.Round(100 * (0.3 * layer1.Score + 0.3 * layer2.Score + 0.4 * layer3.Score), 1);
        return (l1 && l2 && l3, total, layers);
    }
}

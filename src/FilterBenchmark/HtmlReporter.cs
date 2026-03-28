using System.IO;
using System.Linq;

namespace FilterBenchmark;

/// <summary>
/// Writes a self-contained HTML file with six Chart.js charts.
/// No server required — just open benchmark_report.html in any browser.
/// </summary>
public static class HtmlReporter
{
    public static void Write(List<FilterResult> results, int k, string path = "benchmark_report.html")
    {
        var names  = results.Select(r => r.ShortName).ToList();
        var colors = new[] { "#3266ad", "#73726c", "#1D9E75", "#BA7517" };  // blue, gray, teal, amber
        var light  = new[] { "#3266ad33", "#73726c33", "#1D9E7533", "#BA751733" };

        // JS array literals for each metric
        string  jsNames     = ToJsArray(names);
        string  jsBpi       = ToJsArray(results.Select(r => r.BitsPerItem));
        string  jsC         = ToJsArray(results.Select(r => r.OverheadC));
        string  jsFprM      = ToJsArray(results.Select(r => r.MeasuredFpr * 100));
        string  jsFprT      = ToJsArray(results.Select(r => r.TargetFpr  * 100));
        string  jsIns       = ToJsArray(results.Select(r => r.InsertNsPerOp));
        string  jsPos       = ToJsArray(results.Select(r => r.PositiveLookupNsPerOp));
        string  jsNeg       = ToJsArray(results.Select(r => r.NegativeLookupNsPerOp));
        string  jsColors    = "['" + string.Join("','", colors) + "']";

        // Walk-length table rows for the theory section (Walzer 2023 / Schmitz 2025 Table 2)
        string thresholdRows = @"
            <tr><td>Buckets l=2</td><td>0.500</td><td>0.897</td><td>0.959</td><td>0.980</td></tr>
            <tr class='highlight'><td>Windows l=2</td><td>0.500</td><td>0.965</td><td>0.994</td><td>0.999</td></tr>";

        double targetFpr = Math.Pow(2, -k);

        string html = $@"<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""UTF-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1"">
<title>Cuckoo Filter Benchmark Report</title>
<script src=""https://cdnjs.cloudflare.com/ajax/libs/Chart.js/4.4.1/chart.umd.js""></script>
<style>
  *, *::before, *::after {{ box-sizing: border-box; margin: 0; padding: 0; }}
  body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
          background: #f5f5f3; color: #1a1a1a; line-height: 1.6; font-size: 15px; }}
  .page  {{ max-width: 1080px; margin: 0 auto; padding: 2rem 1.5rem; }}
  h1     {{ font-size: 22px; font-weight: 500; margin-bottom: 0.25rem; }}
  h2     {{ font-size: 16px; font-weight: 500; margin-bottom: 0.75rem; color: #333; }}
  .meta  {{ font-size: 13px; color: #666; margin-bottom: 2rem; }}
  .grid  {{ display: grid; grid-template-columns: repeat(auto-fit, minmax(460px, 1fr));
            gap: 1.25rem; margin-bottom: 2rem; }}
  .card  {{ background: #fff; border-radius: 10px; border: 0.5px solid #e0e0da;
            padding: 1.25rem 1.5rem; }}
  .chart-wrap {{ position: relative; width: 100%; }}
  .insight {{ background: #eaf3de; border-left: 3px solid #1D9E75;
              border-radius: 0 6px 6px 0; padding: 9px 14px;
              font-size: 13px; color: #27500A; margin-bottom: 1rem; }}
  table  {{ width: 100%; border-collapse: collapse; font-size: 13px; }}
  th, td {{ padding: 7px 12px; text-align: right; border-bottom: 0.5px solid #e8e8e4; }}
  th     {{ font-weight: 500; text-align: center; background: #f9f9f7; }}
  td:first-child, th:first-child {{ text-align: left; }}
  tr.highlight td {{ background: #eaf3de; font-weight: 500; color: #27500A; }}
  .legend {{ display: flex; gap: 1.5rem; margin-bottom: 0.75rem; flex-wrap: wrap; }}
  .leg   {{ display: flex; align-items: center; gap: 6px; font-size: 12px; color: #555; }}
  .sq    {{ width: 10px; height: 10px; border-radius: 2px; flex-shrink: 0; }}
  .note  {{ font-size: 12px; color: #888; margin-top: 0.5rem; }}
  @media(max-width:520px){{ .grid{{ grid-template-columns:1fr; }} }}
</style>
</head>
<body>
<div class=""page"">

  <h1>Cuckoo Filter Benchmark Report</h1>
  <p class=""meta"">n = {results[0].Capacity:N0} keys &nbsp;·&nbsp; k = {k} &nbsp;·&nbsp;
     Target FPR = 2<sup>-{k}</sup> = {targetFpr*100:F4}% &nbsp;·&nbsp;
     Generated {DateTime.Now:yyyy-MM-dd HH:mm}</p>

  <div class=""insight"">
    Four filters compared: Bloom (baseline), Bucketed cuckoo (Fan et al. 2014), Windowed cuckoo
    (Schmitz et al. 2025 — smallest C), Morton (Breslow &amp; Jayasena 2020 — fastest negative lookup
    via occupancy bitmap). Morton uses k+6 bits per fingerprint (wider than cuckoo's k+3/k+2)
    but its bitmap-gated scan skips empty slots, making it fast when the table is sparse.
  </div>

  <div class=""grid"">

    <!-- Chart 1: Bits per item -->
    <div class=""card"">
      <h2>Bits per item (measured)</h2>
      <div class=""legend"">
        <span class=""leg""><span class=""sq"" style=""background:#3266ad""></span>Bloom</span>
        <span class=""leg""><span class=""sq"" style=""background:#73726c""></span>Bucketed (2,4)</span>
        <span class=""leg""><span class=""sq"" style=""background:#1D9E75""></span>Windowed (2,2)</span>
        <span class=""leg""><span class=""sq"" style=""background:#BA7517""></span>Morton</span>
      </div>
      <div class=""chart-wrap"" style=""height:220px""><canvas id=""c1""></canvas></div>
      <p class=""note"">Lower is better. Theoretical minimum = k = {k} bits/item (C = 1.0). Morton's higher value reflects k+6 fingerprint size.</p>
    </div>

    <!-- Chart 2: Overhead factor C -->
    <div class=""card"">
      <h2>Overhead factor C = bits_per_item / k</h2>
      <div class=""legend"">
        <span class=""leg""><span class=""sq"" style=""background:#3266ad""></span>Bloom</span>
        <span class=""leg""><span class=""sq"" style=""background:#73726c""></span>Bucketed (2,4)</span>
        <span class=""leg""><span class=""sq"" style=""background:#1D9E75""></span>Windowed (2,2)</span>
        <span class=""leg""><span class=""sq"" style=""background:#BA7517""></span>Morton</span>
      </div>
      <div class=""chart-wrap"" style=""height:220px""><canvas id=""c2""></canvas></div>
      <p class=""note"">C = 1.0 is theoretical minimum. Bloom ≈ 1.44, bucketed ≈ 1.05(1+3/k), windowed ≈ 1.06(1+2/k), Morton ≈ (k+6)/(k×0.95).</p>
    </div>

    <!-- Chart 3: FPR measured vs target -->
    <div class=""card"">
      <h2>False positive rate — measured vs. target</h2>
      <div class=""legend"">
        <span class=""leg""><span class=""sq"" style=""background:#3266ad""></span>Measured</span>
        <span class=""leg""><span class=""sq"" style=""background:#E24B4A""></span>Target ({targetFpr*100:F4}%)</span>
      </div>
      <div class=""chart-wrap"" style=""height:220px""><canvas id=""c3""></canvas></div>
      <p class=""note"">Measured ≈ target confirms correctness. Measured below target means filter is not completely full.</p>
    </div>

    <!-- Chart 4: Throughput — insert -->
    <div class=""card"">
      <h2>Insert throughput (ns per operation)</h2>
      <div class=""legend"">
        <span class=""leg""><span class=""sq"" style=""background:#3266ad""></span>Bloom</span>
        <span class=""leg""><span class=""sq"" style=""background:#73726c""></span>Bucketed (2,4)</span>
        <span class=""leg""><span class=""sq"" style=""background:#1D9E75""></span>Windowed (2,2)</span>
        <span class=""leg""><span class=""sq"" style=""background:#BA7517""></span>Morton</span>
      </div>
      <div class=""chart-wrap"" style=""height:220px""><canvas id=""c4""></canvas></div>
      <p class=""note"">Lower is better. Bloom has no random walk. Morton's larger blocks (32 slots) reduce displacement frequency.</p>
    </div>

    <!-- Chart 5: Negative lookup latency — Morton's key advantage -->
    <div class=""card"">
      <h2>Negative lookup latency (ns/op) — Morton's advantage</h2>
      <div class=""legend"">
        <span class=""leg""><span class=""sq"" style=""background:#3266ad""></span>Bloom</span>
        <span class=""leg""><span class=""sq"" style=""background:#73726c""></span>Bucketed (2,4)</span>
        <span class=""leg""><span class=""sq"" style=""background:#1D9E75""></span>Windowed (2,2)</span>
        <span class=""leg""><span class=""sq"" style=""background:#BA7517""></span>Morton</span>
      </div>
      <div class=""chart-wrap"" style=""height:220px""><canvas id=""c5""></canvas></div>
      <p class=""note"">Morton's occupancy bitmap short-circuits empty blocks — a single uint32 check per block replaces scanning all slots.</p>
    </div>

    <!-- Chart 6: Positive vs negative lookup side-by-side -->
    <div class=""card"">
      <h2>Positive vs. negative lookup (ns/op)</h2>
      <div class=""legend"">
        <span class=""leg""><span class=""sq"" style=""background:#3266ad""></span>Positive (key present)</span>
        <span class=""leg""><span class=""sq"" style=""background:#1D9E75""></span>Negative (key absent)</span>
      </div>
      <div class=""chart-wrap"" style=""height:220px""><canvas id=""c6""></canvas></div>
      <p class=""note"">Cuckoo-family filters check exactly 2 locations regardless. Bloom exits early on first zero bit for negatives.</p>
    </div>

    <!-- Chart 7: Overhead C across k values (theoretical) -->
    <div class=""card"">
      <h2>Theoretical overhead C across FPR targets</h2>
      <div class=""legend"">
        <span class=""leg""><span class=""sq"" style=""background:#3266ad""></span>Bloom (1.443)</span>
        <span class=""leg""><span class=""sq"" style=""background:#73726c""></span>Bucketed 1.05(1+3/k)</span>
        <span class=""leg""><span class=""sq"" style=""background:#1D9E75""></span>Windowed 1.06(1+2/k)</span>
        <span class=""leg""><span class=""sq"" style=""background:#BA7517""></span>Morton (k+6)/(k×0.95)</span>
      </div>
      <div class=""chart-wrap"" style=""height:220px""><canvas id=""c7""></canvas></div>
      <p class=""note"">Morton's C converges toward the cuckoo filters only at large k. Its advantage is speed, not space.</p>
    </div>

  </div>

  <!-- Theory table -->
  <div class=""card"" style=""margin-bottom:2rem"">
    <h2>Load thresholds for (2, l) cuckoo hashing — Walzer 2023</h2>
    <p class=""note"" style=""margin-bottom:0.75rem"">
      Overlapping windows have strictly higher load thresholds than disjoint buckets at every l.
      Higher threshold → less wasted space → smaller overhead C.
    </p>
    <table>
      <thead>
        <tr><th>Layout</th><th>l = 1</th><th>l = 2</th><th>l = 3</th><th>l = 4</th></tr>
      </thead>
      <tbody>{thresholdRows}</tbody>
    </table>
  </div>

</div>

<script>
const names  = {jsNames};
const bpi    = {jsBpi};
const cVals  = {jsC};
const fprM   = {jsFprM};
const fprT   = {jsFprT};
const insNs  = {jsIns};
const posNs  = {jsPos};
const negNs  = {jsNeg};
const colors = {jsColors};

const gridOpts = {{
  responsive: true, maintainAspectRatio: false,
  plugins: {{ legend: {{ display: false }} }},
  scales: {{
    x: {{ grid: {{ color: '#e8e8e422' }}, ticks: {{ font: {{ size: 12 }} }} }},
    y: {{ grid: {{ color: '#e8e8e422' }}, ticks: {{ font: {{ size: 12 }} }} }}
  }}
}};

function barChart(id, labels, data, color, yLabel, minVal) {{
  const opts = JSON.parse(JSON.stringify(gridOpts));
  opts.scales.y.title = {{ display: true, text: yLabel, font: {{ size: 12 }} }};
  if (minVal !== undefined) opts.scales.y.min = minVal;
  new Chart(document.getElementById(id), {{
    type: 'bar',
    data: {{
      labels,
      datasets: [{{ data, backgroundColor: color, borderRadius: 4, barPercentage: 0.55 }}]
    }},
    options: opts
  }});
}}

// Chart 1: bits per item
barChart('c1', names, bpi, colors, 'bits / item', 0);

// Chart 2: overhead C
barChart('c2', names, cVals, colors, 'C', 1.0);

// Chart 3: FPR measured vs target (grouped)
new Chart(document.getElementById('c3'), {{
  type: 'bar',
  data: {{
    labels: names,
    datasets: [
      {{ label: 'Measured', data: fprM, backgroundColor: '#3266ad', borderRadius: 4, barPercentage: 0.4 }},
      {{ label: 'Target',   data: fprT, backgroundColor: '#E24B4a', borderRadius: 4, barPercentage: 0.4 }}
    ]
  }},
  options: {{
    ...gridOpts,
    plugins: {{ legend: {{ display: false }} }},
    scales: {{
      x: {{ grid: {{ color: '#e8e8e422' }}, ticks: {{ font: {{ size:12 }} }} }},
      y: {{ grid: {{ color: '#e8e8e422' }}, ticks: {{
        font: {{ size:12 }}, callback: v => v.toFixed(4) + '%'
      }} }}
    }}
  }}
}});

// Chart 4: insert ns/op
barChart('c4', names, insNs, colors, 'ns / op', 0);

// Chart 5: negative lookup only — highlights Morton's occupancy bitmap advantage
barChart('c5', names, negNs, colors, 'ns / op', 0);

// Chart 6: positive vs negative lookup side-by-side (grouped)
new Chart(document.getElementById('c6'), {{
  type: 'bar',
  data: {{
    labels: names,
    datasets: [
      {{ label: 'Positive', data: posNs, backgroundColor: '#3266ad', borderRadius: 4, barPercentage: 0.4 }},
      {{ label: 'Negative', data: negNs, backgroundColor: '#1D9E75', borderRadius: 4, barPercentage: 0.4 }}
    ]
  }},
  options: {{
    ...gridOpts,
    plugins: {{ legend: {{ display: false }} }},
    scales: {{
      x: {{ grid: {{ color: '#e8e8e422' }}, ticks: {{ font: {{ size:12 }} }} }},
      y: {{ grid: {{ color: '#e8e8e422' }}, ticks: {{ font: {{ size:12 }} }},
           title: {{ display: true, text: 'ns / op', font: {{ size:12 }} }} }}
    }}
  }}
}});

// Chart 7: theoretical overhead C across k values — now includes Morton
const ks = [4,5,6,7,8,9,10,12,14,16,18,20];
new Chart(document.getElementById('c7'), {{
  type: 'line',
  data: {{
    labels: ks.map(k => 'k=' + k),
    datasets: [
      {{ label: 'Bloom',    data: ks.map(() => 1.443),
         borderColor: '#3266ad', borderWidth: 2, pointRadius: 3, tension: 0 }},
      {{ label: 'Bucketed', data: ks.map(k => 1.05*(1+3/k)),
         borderColor: '#73726c', borderWidth: 2, pointRadius: 3, tension: 0.3 }},
      {{ label: 'Windowed', data: ks.map(k => 1.06*(1+2/k)),
         borderColor: '#1D9E75', borderWidth: 2.5, pointRadius: 3, tension: 0.3 }},
      {{ label: 'Morton',   data: ks.map(k => (k+6)/(k*0.95)),
         borderColor: '#BA7517', borderWidth: 2, pointRadius: 3, tension: 0.3,
         borderDash: [5,3] }},
    ]
  }},
  options: {{
    responsive: true, maintainAspectRatio: false,
    plugins: {{ legend: {{ display: false }} }},
    scales: {{
      x: {{ grid: {{ color: '#e8e8e422' }}, ticks: {{ font: {{ size:12 }} }} }},
      y: {{ grid: {{ color: '#e8e8e422' }}, ticks: {{ font: {{ size:12 }}, callback: v => v.toFixed(2) }},
           title: {{ display: true, text: 'C', font: {{ size:12 }} }} }}
    }}
  }}
}});
</script>
</body>
</html>";

        File.WriteAllText(path, html);
        Console.WriteLine($"Report written → {Path.GetFullPath(path)}");
        Console.WriteLine("Open benchmark_report.html in any browser to view the charts.\n");
    }

    private static string ToJsArray(IEnumerable<string> values) =>
        "['" + string.Join("','", values) + "']";

    private static string ToJsArray(IEnumerable<double> values) =>
        "[" + string.Join(",", values.Select(v => v.ToString("F4", System.Globalization.CultureInfo.InvariantCulture))) + "]";
}

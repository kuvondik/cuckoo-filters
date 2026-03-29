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
<script src=""https://cdn.jsdelivr.net/npm/chartjs-plugin-datalabels@2.2.0/dist/chartjs-plugin-datalabels.min.js""></script>
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
      <h2>Negative lookup latency (ns/op)</h2>
      <div class=""legend"">
        <span class=""leg""><span class=""sq"" style=""background:#3266ad""></span>Bloom</span>
        <span class=""leg""><span class=""sq"" style=""background:#73726c""></span>Bucketed (2,4)</span>
        <span class=""leg""><span class=""sq"" style=""background:#1D9E75""></span>Windowed (2,2)</span>
        <span class=""leg""><span class=""sq"" style=""background:#BA7517""></span>Morton</span>
      </div>
      <div class=""chart-wrap"" style=""height:220px""><canvas id=""c5""></canvas></div>
      <p class=""note"" style=""margin-top:0.6rem"">
        Lower is better. Morton leads because its <strong>32-bit occupancy bitmap</strong>
        short-circuits the entire slot scan when a block is empty:<br>
        <code style=""font-size:11px;background:#f0f0ec;padding:2px 6px;border-radius:4px;display:inline-block;margin-top:4px"">
          if (occupancy[block] == 0) return false; &nbsp;// no fingerprint bytes loaded
        </code><br>
        Bloom exits early on the first zero bit for negatives (variable cost).
        Both cuckoo variants always scan all slots in both candidate locations (fixed cost).
      </p>
    </div>

    <!-- Chart 6: Positive lookup throughput — dedicated chart -->
    <div class=""card"">
      <h2>Positive lookup throughput (ns/op)</h2>
      <div class=""legend"">
        <span class=""leg""><span class=""sq"" style=""background:#3266ad""></span>Bloom</span>
        <span class=""leg""><span class=""sq"" style=""background:#73726c""></span>Bucketed (2,4)</span>
        <span class=""leg""><span class=""sq"" style=""background:#1D9E75""></span>Windowed (2,2)</span>
        <span class=""leg""><span class=""sq"" style=""background:#BA7517""></span>Morton</span>
      </div>
      <div class=""chart-wrap"" style=""height:220px""><canvas id=""c6""></canvas></div>
      <p class=""note"">
        Lower is better. All three cuckoo-family filters check exactly 2 locations — similar latency.
        Bloom must verify all k={k} bit positions on a positive result with no early exit,
        making it the slowest here despite being the fastest to insert.
      </p>
    </div>

    <!-- Chart 7: Overhead C across k values (theoretical) -->
    <div class=""card"">
      <h2>Theoretical overhead C(k) across FPR targets</h2>
      <div class=""legend"">
        <span class=""leg""><span class=""sq"" style=""background:#3266ad""></span>Bloom — C = 1.443 (flat)</span>
        <span class=""leg""><span class=""sq"" style=""background:#73726c""></span>Bucketed — 1.05(1+3/k)</span>
        <span class=""leg""><span class=""sq"" style=""background:#1D9E75""></span>Windowed — 1.06(1+2/k)</span>
        <span class=""leg""><span class=""sq"" style=""background:#BA7517""></span>Morton — (k+6)/(k×0.95)</span>
      </div>
      <div class=""chart-wrap"" style=""height:240px""><canvas id=""c7""></canvas></div>
      <table style=""margin-top:0.75rem;font-size:12px;border-collapse:collapse;width:100%"">
        <thead>
          <tr style=""background:#f9f9f7"">
            <th style=""padding:5px 8px;border:0.5px solid #e0e0da;text-align:left"">Filter</th>
            <th style=""padding:5px 8px;border:0.5px solid #e0e0da"">C formula</th>
            <th style=""padding:5px 8px;border:0.5px solid #e0e0da"">C at k=10</th>
            <th style=""padding:5px 8px;border:0.5px solid #e0e0da"">Asymptote (k→∞)</th>
          </tr>
        </thead>
        <tbody>
          <tr><td style=""padding:5px 8px;border:0.5px solid #e8e8e4"">Bloom</td>
              <td style=""padding:5px 8px;border:0.5px solid #e8e8e4;text-align:center"">1.443</td>
              <td style=""padding:5px 8px;border:0.5px solid #e8e8e4;text-align:center"">1.443</td>
              <td style=""padding:5px 8px;border:0.5px solid #e8e8e4;text-align:center"">1.443 (never improves)</td></tr>
          <tr><td style=""padding:5px 8px;border:0.5px solid #e8e8e4"">Bucketed (2,4)</td>
              <td style=""padding:5px 8px;border:0.5px solid #e8e8e4;text-align:center"">1.05(1+3/k)</td>
              <td style=""padding:5px 8px;border:0.5px solid #e8e8e4;text-align:center"">1.365</td>
              <td style=""padding:5px 8px;border:0.5px solid #e8e8e4;text-align:center"">→ 1.05</td></tr>
          <tr style=""background:#eaf3de"">
              <td style=""padding:5px 8px;border:0.5px solid #e8e8e4;font-weight:500;color:#27500A"">Windowed (2,2)</td>
              <td style=""padding:5px 8px;border:0.5px solid #e8e8e4;text-align:center;font-weight:500;color:#27500A"">1.06(1+2/k)</td>
              <td style=""padding:5px 8px;border:0.5px solid #e8e8e4;text-align:center;font-weight:500;color:#27500A"">1.272</td>
              <td style=""padding:5px 8px;border:0.5px solid #e8e8e4;text-align:center;font-weight:500;color:#27500A"">→ 1.06 ✓ lowest</td></tr>
          <tr><td style=""padding:5px 8px;border:0.5px solid #e8e8e4"">Morton</td>
              <td style=""padding:5px 8px;border:0.5px solid #e8e8e4;text-align:center"">(k+6)/(k×0.95)</td>
              <td style=""padding:5px 8px;border:0.5px solid #e8e8e4;text-align:center"">1.684</td>
              <td style=""padding:5px 8px;border:0.5px solid #e8e8e4;text-align:center"">→ 1.05</td></tr>
        </tbody>
      </table>
      <p class=""note"" style=""margin-top:0.5rem"">
        C = actual bits/item ÷ k. Lower is better; 1.0 is the information-theoretic minimum.
        Cuckoo-family filters improve as k grows because the constant overhead bits (+2, +3, +6)
        shrink as a fraction of the total fingerprint. Bloom's C is fixed at 1.443 regardless of k.
        <strong>Crossover:</strong> bucketed cuckoo beats Bloom at k ≈ 7 (FPR ≈ 0.78%).
        Morton's advantage is speed, not space — its C exceeds Bloom for k ≤ 8.
      </p>
    </div>

  </div>

  <!-- Spider / Radar chart — full width, all 5 dimensions -->
  <div class=""card"" style=""margin-bottom:1.25rem"">
    <h2>Multi-dimensional trade-off — spider chart</h2>
    <p class=""note"" style=""margin-bottom:0.75rem"">
      Each axis is normalized to [0, 1] where <strong>higher = better</strong>.
      Space efficiency = 1 − (C − C<sub>min</sub>) / (C<sub>max</sub> − C<sub>min</sub>).
      Throughput axes = 1 − (latency − min) / (max − min).
      Deletion support is binary (0 = not supported, 1 = supported).
    </p>
    <div class=""legend"" style=""margin-bottom:0.5rem"">
      <span class=""leg""><span class=""sq"" style=""background:#3266ad;opacity:0.8""></span>Bloom</span>
      <span class=""leg""><span class=""sq"" style=""background:#73726c;opacity:0.8""></span>Bucketed (2,4)</span>
      <span class=""leg""><span class=""sq"" style=""background:#1D9E75;opacity:0.8""></span>Windowed (2,2)</span>
      <span class=""leg""><span class=""sq"" style=""background:#BA7517;opacity:0.8""></span>Morton</span>
    </div>
    <div class=""chart-wrap"" style=""height:380px;max-width:520px;margin:0 auto"">
      <canvas id=""c8""></canvas>
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

// ── Shared helpers ────────────────────────────────────────────────────────

Chart.register(ChartDataLabels);

const gridOpts = {{
  responsive: true, maintainAspectRatio: false,
  plugins: {{ legend: {{ display: false }}, datalabels: {{ display: false }} }},
  scales: {{
    x: {{ grid: {{ color: '#e8e8e422' }}, ticks: {{ font: {{ size: 12 }} }} }},
    y: {{ grid: {{ color: '#e8e8e422' }}, ticks: {{ font: {{ size: 12 }} }} }}
  }}
}};

function barChart(id, labels, data, color, yLabel, minVal, fmtFn) {{
  const opts = JSON.parse(JSON.stringify(gridOpts));
  opts.scales.y.title = {{ display: true, text: yLabel, font: {{ size: 12 }} }};
  if (minVal !== undefined) opts.scales.y.min = minVal;
  const maxVal = Math.max(...data);
  const base   = minVal !== undefined ? minVal : 0;
  opts.scales.y.max = base + (maxVal - base) * 1.22;
  opts.plugins.datalabels = {{
    display: true, anchor: 'end', align: 'end', offset: 4,
    font: {{ size: 11, weight: '500' }}, color: '#444441',
    formatter: fmtFn || (v => v.toFixed(2))
  }};
  new Chart(document.getElementById(id), {{
    type: 'bar',
    data: {{ labels, datasets: [{{ data, backgroundColor: color, borderRadius: 4, barPercentage: 0.55 }}] }},
    options: opts
  }});
}}

// Chart 1: bits per item
barChart('c1', names, bpi, colors, 'bits / item', 0, v => v.toFixed(2));

// Chart 2: overhead C
barChart('c2', names, cVals, colors, 'C', 1.0, v => v.toFixed(3));

// Chart 3: FPR measured vs target
new Chart(document.getElementById('c3'), {{
  type: 'bar',
  data: {{
    labels: names,
    datasets: [
      {{ label: 'Measured', data: fprM, backgroundColor: '#3266ad', borderRadius: 4,
         barPercentage: 0.4,
         datalabels: {{ display:true, anchor:'end', align:'end', offset:3,
                        font:{{size:10,weight:'500'}}, color:'#0C447C',
                        formatter: v => v.toFixed(4)+'%' }} }},
      {{ label: 'Target', data: fprT, backgroundColor: '#E24B4a', borderRadius: 4,
         barPercentage: 0.4,
         datalabels: {{ display:true, anchor:'end', align:'end', offset:3,
                        font:{{size:10,weight:'500'}}, color:'#791F1F',
                        formatter: v => v.toFixed(4)+'%' }} }}
    ]
  }},
  options: {{
    ...gridOpts,
    plugins: {{ legend:{{display:false}}, datalabels:{{display:true}} }},
    scales: {{
      x: {{ grid:{{color:'#e8e8e422'}}, ticks:{{font:{{size:12}}}} }},
      y: {{ grid:{{color:'#e8e8e422'}},
            ticks:{{font:{{size:12}}, callback: v => v.toFixed(4)+'%'}},
            max: Math.max(...fprM, ...fprT) * 1.35 }}
    }}
  }}
}});

// Charts 4, 5, 6: throughput
barChart('c4', names, insNs, colors, 'ns / op', 0, v => v.toFixed(1));
barChart('c5', names, negNs, colors, 'ns / op', 0, v => v.toFixed(1));
barChart('c6', names, posNs, colors, 'ns / op', 0, v => v.toFixed(1));

// Chart 7: C(k) line chart
const ks = [4,5,6,7,8,9,10,12,14,16,18,20];
const c7Data = {{
  bloom:    ks.map(() => 1.443),
  bucketed: ks.map(k => 1.05*(1+3/k)),
  windowed: ks.map(k => 1.06*(1+2/k)),
  morton:   ks.map(k => (k+6)/(k*0.95))
}};
new Chart(document.getElementById('c7'), {{
  type: 'line',
  data: {{
    labels: ks.map(k => 'k='+k),
    datasets: [
      {{ label:'Bloom',    data:c7Data.bloom,    borderColor:'#3266ad',
         borderWidth:2, pointRadius:2, tension:0,
         datalabels: {{ display:false }} }},
      {{ label:'Bucketed', data:c7Data.bucketed, borderColor:'#73726c',
         borderWidth:2, pointRadius:2, tension:0.3,
         datalabels: {{ display:false }} }},
      {{ label:'Windowed', data:c7Data.windowed, borderColor:'#1D9E75',
         borderWidth:2.5, pointRadius:2, tension:0.3,
         datalabels: {{ display:false }} }},
      {{ label:'Morton',   data:c7Data.morton,   borderColor:'#BA7517',
         borderWidth:2, pointRadius:2, tension:0.3, borderDash:[5,3],
         datalabels: {{ display:false }} }}
    ]
  }},
  options: {{
    responsive:true, maintainAspectRatio:false,
    layout: {{ padding: {{ right: 16, top: 8, bottom: 8 }} }},
    plugins:{{ legend:{{display:false}}, datalabels:{{display:false}} }},
    scales:{{
      x:{{ grid:{{color:'#e8e8e422'}}, ticks:{{font:{{size:12}}}} }},
      y:{{
        grid:{{color:'#e8e8e422'}},
        ticks:{{font:{{size:12}}, callback: v=>v.toFixed(2)}},
        title:{{display:true, text:'Overhead C', font:{{size:12}}}},
        min: 1.0, max: 2.8
      }}
    }}
  }}
}});

// Chart 8: Spider / Radar chart
// FIX: Chart.js 4.x pointLabels does not split plain strings on '\n'.
// Use the pointLabels.callback to return the label directly — Chart.js 4.x
// renders array return values as multi-line natively when using the callback form.
// Extra layout padding stops the axis labels being clipped at the canvas edge.
(function() {{
  const rawSpace = [cVals[0], cVals[1], cVals[2], cVals[3]];
  const rawIns   = [insNs[0], insNs[1], insNs[2], insNs[3]];
  const rawPos   = [posNs[0], posNs[1], posNs[2], posNs[3]];
  const rawNeg   = [negNs[0], negNs[1], negNs[2], negNs[3]];
  const rawDel   = [0, 1, 1, 1];

  function normalize(arr, invert) {{
    const mn = Math.min(...arr), mx = Math.max(...arr);
    if (mx === mn) return arr.map(() => 1);
    return arr.map(v => invert
      ? +(1-(v-mn)/(mx-mn)).toFixed(3)
      : +((v-mn)/(mx-mn)).toFixed(3));
  }}

  const normSpace = normalize(rawSpace, true);
  const normIns   = normalize(rawIns,   true);
  const normPos   = normalize(rawPos,   true);
  const normNeg   = normalize(rawNeg,   true);
  const normDel   = rawDel;

  // Axis labels as arrays — each inner array is one label, rendered on two lines.
  // The pointLabels.callback below returns the array directly; Chart.js 4.x
  // renders each element on its own line inside the label bounding box.
  const axisLabels = [
    ['Space', 'efficiency'],
    ['Insert', 'speed'],
    ['Positive', 'lookup'],
    ['Negative', 'lookup'],
    ['Deletion', 'support']
  ];

  const filters = ['Bloom', 'Bucketed', 'Windowed', 'Morton'];
  const fc = ['#3266ad', '#73726c', '#1D9E75', '#BA7517'];
  const fb = ['#3266ad30', '#73726c30', '#1D9E7530', '#BA751730'];

  const datasets = filters.map((name, i) => ({{
    label: name,
    data: [normSpace[i], normIns[i], normPos[i], normNeg[i], normDel[i]],
    borderColor: fc[i], backgroundColor: fb[i], borderWidth: 2,
    pointBackgroundColor: fc[i],
    pointRadius: 2,          // small dots — don't obscure tick numbers
    pointHoverRadius: 6,     // enlarge on hover so they are still easy to target
    datalabels: {{ display: false }}
  }}));

  new Chart(document.getElementById('c8'), {{
    type: 'radar',
    data: {{ labels: axisLabels, datasets }},
    options: {{
      responsive: true, maintainAspectRatio: false,
      // Padding keeps axis labels from being clipped at canvas edges
      layout: {{ padding: {{ top: 20, bottom: 20, left: 20, right: 20 }} }},
      plugins: {{
        legend: {{ display: false }},
        datalabels: {{ display: false }},
        tooltip: {{
          callbacks: {{
            // Flatten array label back to a readable string in the tooltip title
            title: items => items[0].label.join(' '),
            label: ctx => `${{ctx.dataset.label}}: ${{ctx.raw.toFixed(2)}}`
          }}
        }}
      }},
      scales: {{
        r: {{
          min: 0, max: 1,
          ticks: {{ display: false }},
          pointLabels: {{
            font: {{ size: 12, weight: '500' }},
            color: '#333',
            padding: 12,
            // Callback receives the raw label value (array) and returns it as-is.
            // Chart.js 4.x renders array elements on separate lines automatically.
            callback: label => label
          }},
          grid: {{ color: '#e0e0da' }},
          angleLines: {{ color: '#d0d0cc' }}
        }}
      }}
    }}
  }});
}})();
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

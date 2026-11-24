// The charting library, with ONLY the parts the report's charts use registered: line and bar charts, a category x-axis and a linear value axis, the
// points and lines and bars they are made of, the tooltip, and the fill under an emphasised line. Importing named parts (not `chart.js/auto`) lets the
// build leave out everything else (pie, radar, time scales, the library's own legend and title, ...).
//
// The library is bundled into the package: no CDN, no download at run time, no global that the backoffice would have to provide. This module is the
// only place that imports it, and chart-runtime.ts loads THIS module on demand (a dynamic import), so the library is only fetched when a chart is
// first drawn. The legend is drawn by the chart card as text, which is why the library's Legend is not registered.

import { BarController, BarElement, CategoryScale, Chart, Filler, LineController, LineElement, LinearScale, PointElement, Tooltip } from 'chart.js';

Chart.register(LineController, BarController, LineElement, BarElement, PointElement, CategoryScale, LinearScale, Tooltip, Filler);

export { Chart };

import type { Plugin } from 'chart.js';
import { CHART_INK } from './chart-theme';
import { ColorScheme } from '../../core/services/theme.service';

/** Draws a big value and a caption in the hole of a doughnut chart. */
export function centerTextPlugin(
  scheme: ColorScheme,
  value: string,
  caption: string,
): Plugin<'doughnut'> {
  return {
    id: 'centerText',
    afterDraw(chart) {
      const arc = chart.getDatasetMeta(0).data[0];
      if (!arc) {
        return;
      }
      const { x, y } = arc as unknown as { x: number; y: number };
      const ink = CHART_INK[scheme];
      const { ctx } = chart;
      ctx.save();
      ctx.textAlign = 'center';
      ctx.textBaseline = 'middle';
      ctx.fillStyle = scheme === 'light' ? '#1b1c1f' : '#f0efec';
      ctx.font = '700 24px Inter, Roboto, "Segoe UI", sans-serif';
      ctx.fillText(value, x, y - 8);
      ctx.fillStyle = ink.muted;
      ctx.font = '500 11px Inter, Roboto, "Segoe UI", sans-serif';
      ctx.fillText(caption, x, y + 14);
      ctx.restore();
    },
  };
}

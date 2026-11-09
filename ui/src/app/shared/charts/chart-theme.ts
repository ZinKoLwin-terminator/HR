import type { ChartOptions, Plugin } from 'chart.js';
import { ColorScheme } from '../../core/services/theme.service';

/**
 * Categorical series palette. Fixed order — assign slots in sequence, never
 * cycle. Light and dark are separately stepped sets, validated against this
 * app's card surfaces (white / #111318). Light slots 3–4 are below 3:1 contrast,
 * so every chart ships with a legend and a table view.
 */
export const SERIES_COLORS: Record<ColorScheme, readonly string[]> = {
  light: ['#2a78d6', '#eb6834', '#1baf7a', '#eda100', '#e87ba4', '#008300', '#6250d6', '#e34948'],
  dark: ['#3987e5', '#d95926', '#199e70', '#c98500', '#d55181', '#008300', '#9085e9', '#e66767'],
};

export interface ChartInk {
  surface: string;
  text: string;
  muted: string;
  grid: string;
  axis: string;
  tooltipBg: string;
  tooltipFg: string;
  crosshair: string;
}

export const CHART_INK: Record<ColorScheme, ChartInk> = {
  light: {
    surface: '#ffffff',
    text: '#52514e',
    muted: '#6f6d68',
    grid: '#e1e0d9',
    axis: '#c3c2b7',
    tooltipBg: '#1f1f1d',
    tooltipFg: '#f0efec',
    crosshair: '#898781',
  },
  dark: {
    surface: '#111318',
    text: '#c3c2b7',
    muted: '#a3a29a',
    grid: '#2c2c2a',
    axis: '#383835',
    tooltipBg: '#f0efec',
    tooltipFg: '#0b0b0b',
    crosshair: '#898781',
  },
};

const FONT_FAMILY = 'Inter, Roboto, "Segoe UI", sans-serif';

/** Recessive, hairline chrome shared by all charts. */
export function baseChartOptions(scheme: ColorScheme): ChartOptions {
  const ink = CHART_INK[scheme];
  return {
    responsive: true,
    maintainAspectRatio: false,
    animation: { duration: 250 },
    font: { family: FONT_FAMILY },
    interaction: { mode: 'index', intersect: false },
    layout: { padding: { top: 4, right: 8 } },
    plugins: {
      legend: {
        position: 'top',
        align: 'start',
        labels: {
          color: ink.text,
          font: { family: FONT_FAMILY, size: 12 },
          boxWidth: 10,
          boxHeight: 10,
          useBorderRadius: true,
          borderRadius: 2,
          padding: 14,
        },
      },
      tooltip: {
        backgroundColor: ink.tooltipBg,
        titleColor: ink.tooltipFg,
        bodyColor: ink.tooltipFg,
        footerColor: ink.tooltipFg,
        titleFont: { family: FONT_FAMILY, weight: 600 },
        bodyFont: { family: FONT_FAMILY },
        padding: 10,
        cornerRadius: 8,
        boxPadding: 4,
        usePointStyle: true,
      },
    },
  } as ChartOptions;
}

export function axisStyle(scheme: ColorScheme, showGrid: boolean) {
  const ink = CHART_INK[scheme];
  return {
    border: { color: ink.axis },
    grid: { display: showGrid, color: ink.grid, drawTicks: false },
    ticks: { color: ink.muted, font: { family: FONT_FAMILY, size: 11 }, padding: 6 },
  };
}

/** Vertical hairline at the hovered index (line charts). */
export function crosshairPlugin(scheme: ColorScheme): Plugin {
  return {
    id: 'crosshair',
    afterDatasetsDraw(chart) {
      const active = chart.tooltip?.getActiveElements();
      if (!active?.length) {
        return;
      }
      const { ctx, chartArea } = chart;
      const x = active[0].element.x;
      ctx.save();
      ctx.strokeStyle = CHART_INK[scheme].crosshair;
      ctx.lineWidth = 1;
      ctx.beginPath();
      ctx.moveTo(x, chartArea.top);
      ctx.lineTo(x, chartArea.bottom);
      ctx.stroke();
      ctx.restore();
    },
  };
}

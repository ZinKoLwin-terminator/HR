import { DATE_FORMATS } from '../../core/models/settings.models';
import { setDisplayTimeZone, todayIso } from './date.util';
import { formatDisplayDate, formatMoney, isoDateInZone } from './format.util';

describe('formatDisplayDate', () => {
  const expected: Record<string, string> = {
    'yyyy-MM-dd': '2025-03-07',
    'dd/MM/yyyy': '07/03/2025',
    'MM/dd/yyyy': '03/07/2025',
    'dd MMM yyyy': '07 Mar 2025',
  };

  it('covers every date format option', () => {
    expect(Object.keys(expected).sort()).toEqual([...DATE_FORMATS].sort());
  });

  for (const format of DATE_FORMATS) {
    it(`formats an ISO date as ${format}`, () => {
      expect(formatDisplayDate('2025-03-07', format)).toBe(expected[format]);
    });
  }

  it('does not shift a plain date by the time zone', () => {
    expect(formatDisplayDate('2025-03-07', 'dd/MM/yyyy', 'Pacific/Auckland')).toBe('07/03/2025');
  });

  it('moves an instant to the configured time zone', () => {
    const instant = '2025-03-07T20:00:00Z';
    expect(formatDisplayDate(instant, 'yyyy-MM-dd', 'Asia/Yangon')).toBe('2025-03-08');
    expect(formatDisplayDate(instant, 'yyyy-MM-dd', 'America/New_York')).toBe('2025-03-07');
  });

  it('returns an empty string for missing or invalid values', () => {
    expect(formatDisplayDate(null, 'yyyy-MM-dd')).toBe('');
    expect(formatDisplayDate('', 'yyyy-MM-dd')).toBe('');
    expect(formatDisplayDate('not a date', 'yyyy-MM-dd')).toBe('');
  });
});

describe('formatMoney', () => {
  it('formats in the given currency', () => {
    expect(formatMoney(1234, 'MMK').replace(/\s/g, ' ')).toBe('MMK 1,234');
    expect(formatMoney(1234.5, 'USD', 'en-US', 2)).toBe('$1,234.50');
  });

  it('shows a dash for a missing amount', () => {
    expect(formatMoney(null, 'MMK')).toBe('—');
    expect(formatMoney(Number.NaN, 'MMK')).toBe('—');
  });
});

describe('todayIso with a display time zone', () => {
  afterEach(() => setDisplayTimeZone(null));

  it('uses the configured zone and falls back for an unknown one', () => {
    vi.useFakeTimers();
    try {
      vi.setSystemTime(new Date('2025-03-07T20:00:00Z'));
      setDisplayTimeZone('Asia/Yangon');
      expect(todayIso()).toBe(isoDateInZone(new Date(), 'Asia/Yangon'));
      expect(todayIso()).toBe('2025-03-08');
      setDisplayTimeZone('Not/AZone');
      expect(todayIso()).not.toBe('');
    } finally {
      vi.useRealTimers();
    }
  });
});

import { ExcelFile } from '../models/excel.models';

/** Saves a generated file through the browser (no server path is ever exposed). */
export function downloadFile(file: ExcelFile): void {
  const url = URL.createObjectURL(file.blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = file.fileName;
  link.rel = 'noopener';
  document.body.appendChild(link);
  link.click();
  link.remove();
  // Give the browser a moment to start the download before releasing the object URL.
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

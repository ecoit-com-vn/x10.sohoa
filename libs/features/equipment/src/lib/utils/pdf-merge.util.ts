import { PDFDocument } from 'pdf-lib';

/**
 * Đếm số trang của một file PDF.
 */
export async function getPdfPageCount(file: File): Promise<number> {
  try {
    const buffer = await file.arrayBuffer();
    const pdf = await PDFDocument.load(buffer, { ignoreEncryption: true });
    return pdf.getPageCount();
  } catch {
    return 1;
  }
}

/**
 * Ghép nối file PDF mới vào cuối file PDF gốc.
 */
export async function mergePdfFiles(
  baseFile: File,
  newFile: File,
  outputFileName: string
): Promise<File> {
  const baseBuffer = await baseFile.arrayBuffer();
  const newBuffer = await newFile.arrayBuffer();

  const basePdf = await PDFDocument.load(baseBuffer, { ignoreEncryption: true });
  const newPdf = await PDFDocument.load(newBuffer, { ignoreEncryption: true });

  const copiedPages = await basePdf.copyPages(newPdf, newPdf.getPageIndices());
  copiedPages.forEach((page) => basePdf.addPage(page));

  const mergedBytes = await basePdf.save();
  return new File([mergedBytes.buffer as ArrayBuffer], outputFileName, { type: 'application/pdf' });
}

/**
 * Chèn một file ảnh vào cuối file PDF dưới dạng 1 trang mới.
 */
export async function appendImageToPdf(
  basePdfFile: File,
  imageFile: File,
  outputFileName: string
): Promise<File> {
  const baseBuffer = await basePdfFile.arrayBuffer();
  const basePdf = await PDFDocument.load(baseBuffer, { ignoreEncryption: true });

  const imageBuffer = await imageFile.arrayBuffer();
  const isPng =
    imageFile.type === 'image/png' || imageFile.name.toLowerCase().endsWith('.png');
  const isJpg =
    imageFile.type === 'image/jpeg' ||
    imageFile.name.toLowerCase().endsWith('.jpg') ||
    imageFile.name.toLowerCase().endsWith('.jpeg');

  if (isPng) {
    const image = await basePdf.embedPng(imageBuffer);
    const page = basePdf.addPage([image.width, image.height]);
    page.drawImage(image, { x: 0, y: 0, width: image.width, height: image.height });
  } else if (isJpg) {
    const image = await basePdf.embedJpg(imageBuffer);
    const page = basePdf.addPage([image.width, image.height]);
    page.drawImage(image, { x: 0, y: 0, width: image.width, height: image.height });
  } else {
    // Định dạng khác không hỗ trợ trực tiếp nhúng vào PDF thì giữ nguyên
    return basePdfFile;
  }

  const mergedBytes = await basePdf.save();
  return new File([mergedBytes.buffer as ArrayBuffer], outputFileName, { type: 'application/pdf' });
}

/**
 * Ghép file đợt quét mới vào file đã có.
 */
export async function combineScanFiles(
  existingFile: File,
  incomingFile: File,
  outputFileName: string
): Promise<File> {
  const isExistingPdf =
    existingFile.type === 'application/pdf' || existingFile.name.toLowerCase().endsWith('.pdf');
  const isIncomingPdf =
    incomingFile.type === 'application/pdf' || incomingFile.name.toLowerCase().endsWith('.pdf');

  if (isExistingPdf && isIncomingPdf) {
    return await mergePdfFiles(existingFile, incomingFile, outputFileName);
  }

  if (isExistingPdf && !isIncomingPdf) {
    return await appendImageToPdf(existingFile, incomingFile, outputFileName);
  }

  // Mặc định trả về file mới nhất nếu không ghép được
  return incomingFile;
}

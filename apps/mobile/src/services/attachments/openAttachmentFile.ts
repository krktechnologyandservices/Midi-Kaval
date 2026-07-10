import RNFS from 'react-native-fs';
import {Linking} from 'react-native';
import {attachmentApiService} from './AttachmentApiService';

function blobToBase64(blob: Blob): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onerror = () => reject(reader.error ?? new Error('Could not read file.'));
    reader.onload = () => {
      const result = reader.result as string;
      // result is "data:<mime>;base64,<payload>" — strip the prefix, RNFS.writeFile
      // wants just the base64 payload.
      const commaIndex = result.indexOf(',');
      resolve(commaIndex >= 0 ? result.slice(commaIndex + 1) : result);
    };
    reader.readAsDataURL(blob);
  });
}

/**
 * Downloads a decrypted attachment (an authenticated request, so it can't just be
 * handed to Linking.openURL as a plain link) and saves it to a cache file so the
 * device's native viewer can open it.
 */
export async function openAttachment(attachmentId: string, fileName: string): Promise<void> {
  const blob = await attachmentApiService.download(attachmentId);
  const base64 = await blobToBase64(blob);
  const path = `${RNFS.CachesDirectoryPath}/${attachmentId}-${fileName}`;
  await RNFS.writeFile(path, base64, 'base64');
  await Linking.openURL(`file://${path}`);
}

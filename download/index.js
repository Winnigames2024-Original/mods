const currentWindowParams = new URLSearchParams(window.location.search);
const storageUrl = currentWindowParams.get('storage');

if (storageUrl) {
  if (storageUrl == "gamebanana_storage36") {
    const storageUrlPath = currentWindowParams.get('storagePath');
    if (storageUrlPath) {
      const full_dataUrl = "https://filecache36.gamebanana.com" + storageUrlPath;
      downloadURL(full_dataUrl);
    }
  }
}

function downloadURL(urlFile) {
  const element = document.getElementById("downloadFile");
  const urlsd = new URL(urlFile);
  const pathname = urlsd.pathname;

  element.textContent = "Download File: " + pathname;
  element.href = urlFile;
}

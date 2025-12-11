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

function downloadURL(url) {
  var link = document.createElement("a");
  link.href = url;
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  delete link;
}

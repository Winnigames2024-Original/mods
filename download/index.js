const currentWindowParams = new URLSearchParams(window.location.search);
const storageUrl = currentWindowParams.get('storage');

if (storageUrl) {
 const full_dataUrl = "https://filecache36.gamebanana.com" + storageUrlPath;
  window.location.href = full_dataUrl;
}

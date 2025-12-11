const urlParams = new URLSearchParams(window.location.search);
const storageUrl = urlParams.get('storage');
const storagepath = urlParams.get('storagePath');

const full_dataurl = "https://filecache36.gamebanana.com" + storagepath;
window.location.href = full_dataurl;

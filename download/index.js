const urlParams = new URLSearchParams(window.location.search);
const storage = urlParams.get('storage');
const storagepath = urlParams.get('storagePath');

const full_dataurl = storage + storagepath;
window.location.href = full_dataurl;

const urlParams = new URLSearchParams(window.location.search);
const type = urlParams.get('port');

if (type == "v-slice") {
  window.location.href = "./redirect?url="
}

const urlParams = new URLSearchParams(window.location.search);
const type = urlParams.get('port');

if (type == "v-slice") {
  alert(type + " port");
  window.location.href = "./v-slice"
}

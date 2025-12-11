const urlParams = new URLSearchParams(window.location.search);
const type = urlParams.get('port');

if (type == "v-slice") {
  console.log(type + " port");
  async function loadAndInsertContent(url, targetDivId) {
  try {
    const response = await fetch(url);
    const htmlContent = await response.text(); // Получаем HTML как текст

    const targetDiv = document.getElementById(targetDivId);

    targetDiv.insertAdjacentHTML('beforeend', htmlContent);

  } catch (error) {
    console.error('Ошибка при загрузке или вставке контента:', error);
  }
}


loadAndInsertContent('https://winnigames2024-original.github.io/mods/WinniFunctions/v-slice.html', 'content-container');

}

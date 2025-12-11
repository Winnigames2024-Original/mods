const urlParams = new URLSearchParams(window.location.search);
const type = urlParams.get('port');

if (type == "v-slice") {
  console.log(type + " port");
  loadAndInsertContent('https://winnigames2024-original.github.io/mods/WinniFunctions/v-slice.html', 'content-container');
}
else {
  if (type == "pc") {
    console.log(type + " port");
    loadAndInsertContent('https://winnigames2024-original.github.io/mods/WinniFunctions/pc.html', 'content-container');
  }
  else {
    loadAndInsertContent('https://winnigames2024-original.github.io/mods/404.html', 'content-container');
  }
}


}

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

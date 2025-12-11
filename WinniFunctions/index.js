const urlParams = new URLSearchParams(window.location.search);
const type = urlParams.get('port');

if (type == "v-slice") {
  alert(type + " port");
  
fetch('https://winnigames2024-original.github.io/mods/WinniFunctions/v-slice.html')
    .then(response => response.text())
    .then(htmlContent => {
        // 2. Находим элемент, куда вставлять
        const container = document.getElementById('myContainer');
        // 3. Вставляем HTML-код
        container.insertAdjacentHTML('beforebegin = htmlContent; // Или container.insertAdjacentHTML('beforeend', htmlContent);
    })
    .catch(error => console.error('Ошибка:', error));

}

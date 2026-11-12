</main>
<footer class="foot">
  <div>© <?= date('Y') ?> NareLauncher · бесплатный лаунчер Minecraft</div>
  <nav>
    <a href="/home/">Главная</a>
    <a href="/versions/">Версии</a>
    <a href="/download/">Скачать</a>
    <?php if (me()): ?><a href="/profile/my/">Профиль</a><?php endif; ?>
  </nav>
</footer>
<script>
(function(){
  var btn=document.getElementById('designToggle'); if(!btn)return;
  function label(){ btn.textContent=document.documentElement.classList.contains('simple')?'◑ Обычный дизайн':'◑ Простой дизайн'; }
  label();
  btn.addEventListener('click',function(){
    var s=document.documentElement.classList.toggle('simple');
    try{localStorage.setItem('nareDesign',s?'simple':'normal');}catch(e){}
    label();
  });
})();
</script>
</body>
</html>

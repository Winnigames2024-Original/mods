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
</body>
</html>

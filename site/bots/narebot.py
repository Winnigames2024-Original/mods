"""
narebot — библиотека для ботов NareLauncher (чаты каналов и трансляций).

Пример bot.py:

    from narebot import Bot

    bot = Bot("ТОКЕН_БОТА")          # токен берётся в редакторе ботов на сайте

    @bot.on_message
    def reply(msg):
        if msg.text.lower() == "привет":
            msg.reply("Привет, " + msg.from_ + "!")

    bot.run()

Запуск:  python bot.py
Файлов в боте может быть сколько угодно — импортируйте свои модули как обычно.
"""
import time
import json
import urllib.request

SERVER = "https://narelauncher.winnigames2024.ru"


class Message:
    def __init__(self, bot, ev):
        self.text = ev.get("text", "")
        self.from_ = ev.get("from", "")
        self.target = ev.get("target", "")   # 'channel:ID' или 'stream:ID'
        self.at = ev.get("at", 0)
        self._bot = bot

    def reply(self, text):
        """Ответить в тот же чат, откуда пришло сообщение."""
        return self._bot.send(self.target, text)

    def mute(self, user, seconds=300):
        """Временно заблокировать пользователю чат (по умолчанию 5 минут)."""
        return self._bot.mod(self.target, "mute", user, seconds)

    def ban(self, user):
        """Забанить пользователя в этом чате навсегда (не сможет писать/заходить)."""
        return self._bot.mod(self.target, "ban", user)

    def unban(self, user):
        """Снять блокировку/бан."""
        return self._bot.mod(self.target, "unban", user)


class Bot:
    def __init__(self, token, server=SERVER):
        self.token = token
        self.server = server.rstrip("/")
        self._handlers = []
        self._since = 0

    def on_message(self, fn):
        """Декоратор: функция будет вызвана на каждое новое сообщение в чатах бота."""
        self._handlers.append(fn)
        return fn

    def _post(self, path, data):
        req = urllib.request.Request(
            self.server + path,
            data=json.dumps(data).encode("utf-8"),
            headers={"Content-Type": "application/json"},
        )
        with urllib.request.urlopen(req, timeout=25) as r:
            return json.loads(r.read().decode("utf-8"))

    def send(self, target, text):
        """Отправить сообщение в канал/трансляцию ('channel:ID' или 'stream:ID')."""
        return self._post("/bot/send", {"botToken": self.token, "target": target, "text": text})

    def mod(self, target, action, user, seconds=0):
        """Модерация: action = 'mute' (seconds), 'ban' (навсегда), 'unban'."""
        return self._post("/bot/mod", {"botToken": self.token, "target": target, "action": action, "user": user, "seconds": seconds})

    def me(self):
        return self._post("/bot/me", {"botToken": self.token})

    def run(self, interval=2):
        info = self.me()
        if not info.get("ok"):
            raise RuntimeError("не удалось войти: " + str(info.get("error")))
        print("Бот '%s' запущен. Цели: %s" % (info.get("name"), info.get("targets")))
        while True:
            try:
                r = self._post("/bot/events", {"botToken": self.token, "since": self._since})
                self._since = r.get("seq", self._since)
                for ev in r.get("events", []):
                    msg = Message(self, ev)
                    for h in self._handlers:
                        try:
                            h(msg)
                        except Exception as e:
                            print("ошибка в обработчике:", e)
            except Exception as e:
                print("сеть:", e)
            time.sleep(interval)

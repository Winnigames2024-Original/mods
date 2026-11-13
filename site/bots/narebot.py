"""
narebot — библиотека для ботов NareLauncher (чаты каналов и трансляций).

НОВЫЙ API (как просили):

    from narebot import Bot      # или: import narebot as NareLauncher; Bot = NareLauncher.Bot

    currentbot = Bot.Bot(Bot.TOKEN("ТОКЕН_БОТА"))   # токен — в редакторе ботов на сайте

    @Bot.Messages.Listener
    def on_message(message, userid):
        if Bot.MessageContains(message, "пр") or Bot.MessageContains(message, "привет"):
            Bot.Messages.Write("привет!", userid)
        # отправить код в рамке с кнопкой «копировать»:
        if Bot.MessageContains(message, "код"):
            code = Bot.Code.python("print('hi from NareBot')")
            Bot.Messages.Write(code, userid)

    currentbot.run()     # можно не писать — запустится само при выходе

СТАРЫЙ API тоже работает:

    bot = Bot("ТОКЕН")
    @bot.on_message
    def reply(msg):
        if msg.text.lower() == "привет": msg.reply("Привет, " + msg.from_ + "!")
    bot.run()

Запуск:  python bot.py
`userid` — это адрес чата, откуда пришло сообщение ('channel:ID' или 'stream:ID');
Bot.Messages.Write(text, userid) отвечает именно туда.
"""
import time
import json
import atexit
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
        return self._bot.send(self.target, text)

    def mute(self, user, seconds=300):
        return self._bot.mod(self.target, "mute", user, seconds)

    def ban(self, user):
        return self._bot.mod(self.target, "ban", user)

    def unban(self, user):
        return self._bot.mod(self.target, "unban", user)


class _BotInstance:
    def __init__(self, token, server=SERVER):
        self.token = token
        self.server = server.rstrip("/")
        self._handlers = []       # функции (msg) старого стиля
        self._handlers2 = []      # функции (text, userid) нового стиля
        self._since = 0
        self._running = False
        _Namespace._current = self      # запоминаем «текущего» бота для Bot.Messages.*

    def on_message(self, fn):
        self._handlers.append(fn)
        return fn

    def _post(self, path, data):
        req = urllib.request.Request(self.server + path,
            data=json.dumps(data).encode("utf-8"),
            headers={"Content-Type": "application/json"})
        with urllib.request.urlopen(req, timeout=25) as r:
            return json.loads(r.read().decode("utf-8"))

    def send(self, target, text):
        return self._post("/bot/send", {"botToken": self.token, "target": target, "text": text})

    def mod(self, target, action, user, seconds=0):
        return self._post("/bot/mod", {"botToken": self.token, "target": target,
                                        "action": action, "user": user, "seconds": seconds})

    def me(self):
        return self._post("/bot/me", {"botToken": self.token})

    def run(self, interval=2):
        if self._running:
            return
        self._running = True
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
                        try: h(msg)
                        except Exception as e: print("ошибка в обработчике:", e)
                    for h in self._handlers2:
                        try: h(msg.text, msg.target)   # (message, userid)
                        except Exception as e: print("ошибка в обработчике:", e)
            except Exception as e:
                print("сеть:", e)
            time.sleep(interval)


class _Messages:
    @staticmethod
    def Listener(fn):
        """Декоратор: @Bot.Messages.Listener над функцией (message, userid)."""
        if _Namespace._current is not None:
            _Namespace._current._handlers2.append(fn)
        else:
            _Namespace._pending.append(fn)
        return fn

    @staticmethod
    def Write(text, userid):
        """Написать в чат userid ('channel:ID' или 'stream:ID')."""
        bot = _Namespace._current
        if bot is None:
            raise RuntimeError("сначала создайте бота: currentbot = Bot.Bot(Bot.TOKEN('...'))")
        return bot.send(userid, text)


def _codeblock(lang, code):
    """Код в рамке с кнопкой «копировать» (в чате NareLauncher)."""
    return "```" + str(lang) + "\n" + str(code) + "\n```"


class _Code:
    @staticmethod
    def python(code): return _codeblock("python", code)
    @staticmethod
    def CSharp(code): return _codeblock("csharp", code)
    @staticmethod
    def csharp(code): return _codeblock("csharp", code)
    @staticmethod
    def js(code): return _codeblock("js", code)
    @staticmethod
    def php(code): return _codeblock("php", code)
    @staticmethod
    def java(code): return _codeblock("java", code)
    @staticmethod
    def html(code): return _codeblock("html", code)
    @staticmethod
    def css(code): return _codeblock("css", code)
    @staticmethod
    def text(code): return _codeblock("text", code)


class _Namespace:
    """Объект `Bot`: и фабрика бота Bot.Bot(...), и пространство имён Bot.Messages/Code/..."""
    _current = None
    _pending = []

    Bot = _BotInstance
    Messages = _Messages
    Code = _Code

    def __call__(self, token, server=SERVER):      # старый стиль: Bot("token")
        return _BotInstance(token, server)

    @staticmethod
    def TOKEN(token):
        return token

    @staticmethod
    def ListenMessages(bot, fn):
        """Bot.ListenMessages(currentbot, func) — зарегистрировать обработчик (message, userid)."""
        bot._handlers2.append(fn)
        return fn

    @staticmethod
    def MessageContains(message, needle):
        return str(needle).lower() in str(message).lower()

    # распространённая опечатка из примера — пусть тоже работает
    MessageContaints = MessageContains


Bot = _Namespace()


def _autorun():
    # если создан бот и есть обработчики, но run() не вызвали — запустим сами
    bot = _Namespace._current
    if bot is None or bot._running or not (bot._handlers or bot._handlers2):
        return
    for fn in _Namespace._pending:
        bot._handlers2.append(fn)
    try:
        bot.run()
    except KeyboardInterrupt:
        pass
    except Exception as e:
        print("Бот остановлен:", e)


atexit.register(_autorun)

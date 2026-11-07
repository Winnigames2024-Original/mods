package com.winnigames2024.forge_narevisuals.client;

import com.winnigames2024.forge_narevisuals.Config;
import com.winnigames2024.forge_narevisuals.Forge_narevisuals;
import com.winnigames2024.forge_narevisuals.Launch;
import net.minecraft.client.Minecraft;
import net.minecraft.world.phys.Vec3;

import javax.sound.sampled.AudioFormat;
import javax.sound.sampled.AudioSystem;
import javax.sound.sampled.DataLine;
import javax.sound.sampled.Mixer;
import javax.sound.sampled.SourceDataLine;
import javax.sound.sampled.TargetDataLine;
import java.util.ArrayDeque;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/**
 * Голосовой чат. Своя реализация в том же жанре, что и известные моды, —
 * чужой код не брался.
 *
 * Как устроено. Микрофон читается своим потоком: 16 кГц, моно, кадрами по
 * 40 мс. Кадр ужимается своим ADPCM — вчетверо, без единой сторонней
 * библиотеки, — и уходит на сервер обычным каналом плагинов, по той же
 * связи, что и сама игра. Отдельный порт открывать не нужно, и это главная
 * причина такого выбора: настроить сервер не придётся вообще.
 *
 * Обратно приходят чужие кадры вместе с координатами говорящего. Они
 * складываются в короткую очередь на каждого — чтобы сетевые рывки не
 * щёлкали, — а затем микшируются в один стереопоток: громкость по
 * расстоянию, баланс по тому, слева или справа человек стоит.
 *
 * Цена решения честная: звук идёт по TCP вместе с игрой, поэтому при плохой
 * связи он будет запаздывать сильнее, чем у голосовых чатов со своим портом
 * по UDP.
 */
public final class NareVoice {

    // 16 кГц моно — голосу хватает с запасом, а трафика вчетверо меньше, чем у 44.1
    public static final int RATE = 16000;
    public static final int FRAME = 640;          // 40 мс
    public static final int MAX_SPEAKERS = 8;

    private static final AudioFormat MIC =
            new AudioFormat(RATE, 16, 1, true, false);
    private static final AudioFormat OUT =
            new AudioFormat(RATE, 16, 2, true, false);

    /** Один говорящий: очередь кадров и где он стоит. */
    private static final class Speaker {
        final String name;
        final ArrayDeque<short[]> frames = new ArrayDeque<>();
        double x, y, z;
        long heard;
        boolean playing;

        Speaker(String name) { this.name = name; }
    }

    private static final Map<String, Speaker> speakers = new LinkedHashMap<>();

    private static Thread micThread, mixThread;
    private static volatile boolean running;

    private static volatile boolean talking;
    private static volatile boolean toggled;
    private static volatile float lastLevel;

    private static volatile String trouble = "";

    private NareVoice() { }

    // --------------------------------------------------------------- запуск

    /** Включает голос, если он нужен и разрешён. Зовётся при входе в мир. */
    public static synchronized void start() {
        if (running) return;
        if (!Launch.visuals()) return;
        if (!Config.get().voiceEnabled) return;

        running = true;
        trouble = "";

        micThread = new Thread(NareVoice::micLoop, "NareVoice-mic");
        micThread.setDaemon(true);
        micThread.start();

        mixThread = new Thread(NareVoice::mixLoop, "NareVoice-mix");
        mixThread.setDaemon(true);
        mixThread.start();

        Forge_narevisuals.LOGGER.info("{}: голос включён", Forge_narevisuals.NAME);
    }

    public static synchronized void stop() {
        running = false;
        talking = false;
        toggled = false;

        synchronized (speakers) { speakers.clear(); }

        micThread = null;
        mixThread = null;
    }

    public static boolean running() { return running; }

    public static String trouble() { return trouble; }

    // ------------------------------------------------------------ говорим

    /** Клавиша зажата или отпущена — решает, передаём ли мы сейчас. */
    public static void hold(boolean down) {
        Config cfg = Config.get();

        if (cfg.voiceMode == 1) {
            if (down) toggled = !toggled;
            return;
        }

        talking = down;
    }

    /** Передаём ли мы голос прямо сейчас. */
    public static boolean sending() {
        if (!running) return false;

        Config cfg = Config.get();
        if (cfg.voiceMuteMic) return false;
        if (cfg.voiceMode == 1) return toggled;
        if (cfg.voiceMode == 2) return lastLevel * 100f >= cfg.voiceThreshold;
        return talking;
    }

    /** Уровень микрофона 0..1 — для полоски в настройках. */
    public static float level() { return lastLevel; }

    /**
     * За сколько блоков мы сейчас слышим. Решает сервер: его слово — потолок,
     * игрок может только убавить. 0 от сервера значит «везде», и тогда
     * расстояние вообще не глушит голос.
     */
    public static int hearing() {
        Config cfg = Config.get();
        int server = NareNet.voiceDistance();

        // сервер молчит (старый плагин или одиночная игра) — как настроено у себя
        if (server < 0) return Math.max(1, cfg.voiceDistance);

        int limit = server == 0 ? Integer.MAX_VALUE : server;

        if (cfg.voiceDistanceCustom) return Math.max(1, Math.min(cfg.voiceDistance, limit));
        return limit;
    }

    /** Слышно ли везде — для подписи в меню. */
    public static boolean hearingEverywhere() {
        return hearing() == Integer.MAX_VALUE;
    }

    // --------------------------------------------------------- по людям

    /** Громкость человека в процентах: 100 — обычная, 0 — заглушён. */
    public static int personVolume(String who) {
        Integer value = Config.get().voicePeople.get(who);
        if (value == null) return 100;
        return value < 0 ? 0 : Math.min(200, value);
    }

    public static boolean muted(String who) {
        Integer value = Config.get().voicePeople.get(who);
        return value != null && value <= 0;
    }

    public static void setPersonVolume(String who, int percent) {
        if (who == null || who.isEmpty()) return;

        percent = Math.max(0, Math.min(200, percent));

        if (percent == 100) Config.get().voicePeople.remove(who);
        else Config.get().voicePeople.put(who, percent);

        Config.save();
    }

    public static void toggleMuted(String who) {
        setPersonVolume(who, muted(who) ? 100 : 0);
    }

    /**
     * Кого показываем в меню: тех, кого слышно сейчас, плюс тех, кому уже
     * выкрутили свою громкость — иначе её нельзя вернуть, пока человек молчит.
     */
    public static List<String> known() {
        var list = new ArrayList<String>();

        synchronized (speakers) {
            for (Speaker speaker : speakers.values()) list.add(speaker.name);
        }

        for (String who : Config.get().voicePeople.keySet())
            if (!list.contains(who)) list.add(who);

        return list;
    }

    // ----------------------------------------------------------- микрофон

    private static void micLoop() {
        // микрофон мог быть занят другой программой или воткнут секунду назад:
        // не сдаёмся с первой попытки, ждём и пробуем ещё
        TargetDataLine line = null;
        String why = null;

        for (int attempt = 1; attempt <= 3 && running && line == null; attempt++) {
            try { line = openMic(); }
            catch (Exception e) { why = reason(e); }

            if (line != null) break;

            try { Thread.sleep(attempt * 700L); }
            catch (InterruptedException stop) { return; }
        }

        if (line == null) {
            if (!running) return;

            trouble = why == null ? "микрофон не открылся" : why;

            NareErrors.report("voice.mic",
                    "Микрофон не открылся",
                    (why == null ? "Windows не дала мод доступ к микрофону." : why)
                    + "\n\nЧаще всего помогает одно из трёх: разрешить приложениям микрофон "
                    + "в настройках Windows, закрыть программу, которая его занимает "
                    + "(Discord, браузер, OBS), или выбрать другой микрофон в меню голоса "
                    + "по клавише " + NareKeys.VOICE.getTranslatedKeyMessage().getString()
                    + ". Остальное в моде работает как обычно — просто вас не слышно.");
            return;
        }

        try {
            line.start();
            trouble = "";

            byte[] raw = new byte[FRAME * 2];
            short[] pcm = new short[FRAME];
            byte[] packed = new byte[FRAME / 2 + 3];

            var codec = new Adpcm();

            while (running) {
                int read = 0;
                while (read < raw.length && running) {
                    int got = line.read(raw, read, raw.length - read);
                    if (got <= 0) break;
                    read += got;
                }
                if (read < raw.length) continue;

                float gain = Math.max(0f, Config.get().voiceMicGain / 100f);
                float peak = 0f;

                for (int i = 0; i < FRAME; i++) {
                    int value = (raw[i * 2] & 0xFF) | (raw[i * 2 + 1] << 8);
                    value = (int) (value * gain);
                    if (value > 32767) value = 32767;
                    if (value < -32768) value = -32768;

                    pcm[i] = (short) value;

                    float a = Math.abs(value) / 32768f;
                    if (a > peak) peak = a;
                }

                // уровень падает плавно: дёрганая полоска бесполезна
                lastLevel = Math.max(peak, lastLevel * 0.82f);

                if (!sending()) { codec.reset(); continue; }

                codec.encode(pcm, packed);
                NareNet.sendVoice(packed);
            }
        }
        catch (Exception e) {
            if (!running) return;

            trouble = reason(e);
            Forge_narevisuals.LOGGER.warn("{}: микрофон: {}", Forge_narevisuals.NAME, trouble);

            NareErrors.report("voice.mic.lost",
                    "Микрофон замолчал",
                    trouble + "\n\nПохоже, устройство отключили или его забрала другая программа. "
                    + "Выберите микрофон заново в меню голоса — клавиша "
                    + NareKeys.VOICE.getTranslatedKeyMessage().getString() + ".");
        }
        finally {
            if (line != null) { try { line.stop(); line.close(); } catch (Exception ignored) { } }
        }
    }

    /** Сообщение исключения, а если его нет — хотя бы имя класса. */
    private static String reason(Exception e) {
        String text = e.getMessage();
        if (text != null && !text.isBlank()) return text;

        if (e instanceof javax.sound.sampled.LineUnavailableException)
            return "микрофон занят другой программой";

        return e.getClass().getSimpleName();
    }

    private static TargetDataLine openMic() throws Exception {
        var info = new DataLine.Info(TargetDataLine.class, MIC);
        String want = Config.get().voiceDevice;

        if (want != null && !want.isEmpty()) {
            for (Mixer.Info mixer : AudioSystem.getMixerInfo()) {
                if (!mixer.getName().equals(want)) continue;
                try {
                    var line = (TargetDataLine) AudioSystem.getMixer(mixer).getLine(info);
                    line.open(MIC, FRAME * 4);
                    return line;
                } catch (Exception ignored) { }
            }
        }

        // системный по умолчанию
        if (AudioSystem.isLineSupported(info)) {
            try {
                var line = (TargetDataLine) AudioSystem.getLine(info);
                line.open(MIC, FRAME * 4);
                return line;
            } catch (Exception ignored) { }
        }

        // и наконец просто любой, который согласится открыться
        for (Mixer.Info mixer : AudioSystem.getMixerInfo()) {
            try {
                var candidate = AudioSystem.getMixer(mixer);
                if (!candidate.isLineSupported(info)) continue;

                var line = (TargetDataLine) candidate.getLine(info);
                line.open(MIC, FRAME * 4);

                Forge_narevisuals.LOGGER.info("{}: микрофон — {}",
                        Forge_narevisuals.NAME, mixer.getName());
                return line;
            } catch (Exception ignored) { }
        }

        return null;
    }

    /** Все микрофоны, какие видит система. */
    public static List<String> microphones() {
        var list = new ArrayList<String>();
        var info = new DataLine.Info(TargetDataLine.class, MIC);

        for (Mixer.Info mixer : AudioSystem.getMixerInfo()) {
            try {
                if (AudioSystem.getMixer(mixer).isLineSupported(info))
                    list.add(mixer.getName());
            } catch (Exception ignored) { }
        }

        return list;
    }

    // ------------------------------------------------------------ приём

    /** Пришёл кадр от другого игрока. */
    public static void incoming(String who, double x, double y, double z, byte[] packed) {
        if (!running) return;

        Minecraft mc = Minecraft.getInstance();
        if (mc.player != null && who.equals(mc.player.getGameProfile().getName())) return;

        short[] pcm = new short[FRAME];
        new Adpcm().decode(packed, pcm);

        synchronized (speakers) {
            Speaker speaker = speakers.get(who);
            if (speaker == null) {
                if (speakers.size() >= MAX_SPEAKERS) return;
                speaker = new Speaker(who);
                speakers.put(who, speaker);
            }

            speaker.x = x;
            speaker.y = y;
            speaker.z = z;
            speaker.heard = System.currentTimeMillis();

            // очередь короткая: лучше проглотить кадр, чем копить задержку
            if (speaker.frames.size() > 8) speaker.frames.pollFirst();
            speaker.frames.addLast(pcm);
        }
    }

    /** Кто сейчас говорит — для элемента HUD. */
    public static List<String> talkers() {
        var list = new ArrayList<String>();
        long now = System.currentTimeMillis();

        synchronized (speakers) {
            for (Speaker speaker : speakers.values())
                if (now - speaker.heard < 600) list.add(speaker.name);
        }

        return list;
    }

    // --------------------------------------------------------- воспроизведение

    private static void mixLoop() {
        SourceDataLine line = null;

        try {
            var info = new DataLine.Info(SourceDataLine.class, OUT);
            if (!AudioSystem.isLineSupported(info)) {
                trouble = "вывод звука не открылся";

                NareErrors.report("voice.out",
                        "Голос некуда выводить",
                        "Система не дала моду звуковой выход на 16 кГц. Говорить вы сможете, "
                        + "а вот других слышно не будет. Обычно это лечится выбором рабочего "
                        + "устройства вывода в настройках звука Windows.");
                return;
            }

            line = (SourceDataLine) AudioSystem.getLine(info);
            line.open(OUT, FRAME * 8);
            line.start();

            int[] mix = new int[FRAME * 2];
            byte[] out = new byte[FRAME * 4];

            while (running) {
                java.util.Arrays.fill(mix, 0);
                boolean any = false;

                Minecraft mc = Minecraft.getInstance();
                Vec3 ear = mc.player == null ? Vec3.ZERO : mc.player.getEyePosition();
                float yaw = mc.player == null ? 0f : mc.player.getYRot();
                float volume = Config.get().voiceMuteAll
                        ? 0f : Math.max(0f, Config.get().voiceVolume / 100f);
                int hearing = hearing();

                synchronized (speakers) {
                    var it = speakers.values().iterator();
                    while (it.hasNext()) {
                        Speaker speaker = it.next();

                        // молчит дольше трёх секунд — забываем
                        if (System.currentTimeMillis() - speaker.heard > 3000
                                && speaker.frames.isEmpty()) {
                            it.remove();
                            continue;
                        }

                        // ждём пару кадров, иначе первые слова щёлкают
                        if (!speaker.playing) {
                            if (speaker.frames.size() < 2) continue;
                            speaker.playing = true;
                        }

                        short[] frame = speaker.frames.pollFirst();
                        if (frame == null) { speaker.playing = false; continue; }

                        double dx = speaker.x - ear.x;
                        double dy = speaker.y - ear.y;
                        double dz = speaker.z - ear.z;
                        double dist = Math.sqrt(dx * dx + dy * dy + dz * dz);

                        // «везде» — громкость одинаковая, панорама всё равно остаётся
                        float near = hearing == Integer.MAX_VALUE
                                ? 1f
                                : (float) Math.max(0.0, 1.0 - dist / hearing);
                        near *= near;                       // вблизи громко, вдали быстро тише

                        // своя громкость этого человека поверх общей
                        float gain = near * volume * (personVolume(speaker.name) / 100f);
                        if (gain <= 0.001f) continue;

                        // слева или справа: угол между взглядом и направлением на голос
                        double angle = Math.atan2(dz, dx) - Math.toRadians(yaw + 90.0);
                        float side = (float) Math.sin(angle);
                        float left = gain * (1f - 0.45f * side);
                        float right = gain * (1f + 0.45f * side);

                        for (int i = 0; i < FRAME; i++) {
                            mix[i * 2] += (int) (frame[i] * left);
                            mix[i * 2 + 1] += (int) (frame[i] * right);
                        }

                        any = true;
                    }
                }

                for (int i = 0; i < mix.length; i++) {
                    int value = mix[i];
                    if (value > 32767) value = 32767;
                    if (value < -32768) value = -32768;

                    out[i * 2] = (byte) (value & 0xFF);
                    out[i * 2 + 1] = (byte) ((value >> 8) & 0xFF);
                }

                if (any) line.write(out, 0, out.length);
                else Thread.sleep(20);
            }
        }
        catch (Exception e) {
            if (!running) return;

            trouble = reason(e);
            Forge_narevisuals.LOGGER.warn("{}: вывод голоса: {}", Forge_narevisuals.NAME, trouble);

            NareErrors.report("voice.out.lost", "Звук голоса пропал",
                    trouble + "\n\nВывод звука закрылся на ходу — бывает, когда меняют "
                    + "устройство вывода. Переоткройте меню голоса, чтобы попробовать снова.");
        }
        finally {
            if (line != null) { try { line.stop(); line.close(); } catch (Exception ignored) { } }
        }
    }

    // ---------------------------------------------------------------- кодек

    /**
     * IMA ADPCM: четыре бита на отсчёт вместо шестнадцати.
     *
     * Взято это не ради качества, а ради независимости: ни одной сторонней
     * библиотеки, ни одного родного файла — значит, мод остаётся одним jar и
     * ставится куда угодно. Для речи четырёхкратного сжатия хватает.
     */
    static final class Adpcm {

        private static final int[] STEPS = {
                7, 8, 9, 10, 11, 12, 13, 14, 16, 17, 19, 21, 23, 25, 28, 31, 34, 37, 41, 45,
                50, 55, 60, 66, 73, 80, 88, 97, 107, 118, 130, 143, 157, 173, 190, 209, 230,
                253, 279, 307, 337, 371, 408, 449, 494, 544, 598, 658, 724, 796, 876, 963,
                1060, 1166, 1282, 1411, 1552, 1707, 1878, 2066, 2272, 2499, 2749, 3024, 3327,
                3660, 4026, 4428, 4871, 5358, 5894, 6484, 7132, 7845, 8630, 9493, 10442,
                11487, 12635, 13899, 15289, 16818, 18500, 20350, 22385, 24623, 27086, 29794,
                32767
        };

        private static final int[] TABLE = { -1, -1, -1, -1, 2, 4, 6, 8 };

        private int predictor;
        private int index;

        void reset() { predictor = 0; index = 0; }

        void encode(short[] pcm, byte[] out) {
            out[0] = (byte) (predictor & 0xFF);
            out[1] = (byte) ((predictor >> 8) & 0xFF);
            out[2] = (byte) index;

            for (int i = 0; i < pcm.length; i += 2) {
                int a = step(pcm[i]);
                int b = step(pcm[i + 1]);
                out[3 + i / 2] = (byte) ((b << 4) | a);
            }
        }

        private int step(short sample) {
            int stepSize = STEPS[index];
            int diff = sample - predictor;

            int code = 0;
            if (diff < 0) { code = 8; diff = -diff; }

            int delta = stepSize >> 3;

            if (diff >= stepSize) { code |= 4; diff -= stepSize; delta += stepSize; }
            if (diff >= (stepSize >> 1)) { code |= 2; diff -= stepSize >> 1; delta += stepSize >> 1; }
            if (diff >= (stepSize >> 2)) { code |= 1; delta += stepSize >> 2; }

            predictor += (code & 8) != 0 ? -delta : delta;
            clamp();

            index += TABLE[code & 7];
            if (index < 0) index = 0;
            if (index > 88) index = 88;

            return code;
        }

        void decode(byte[] in, short[] pcm) {
            predictor = (short) ((in[0] & 0xFF) | (in[1] << 8));
            index = in[2];
            if (index < 0) index = 0;
            if (index > 88) index = 88;

            for (int i = 0; i < pcm.length; i += 2) {
                int packed = in[3 + i / 2] & 0xFF;
                pcm[i] = back(packed & 0x0F);
                pcm[i + 1] = back((packed >> 4) & 0x0F);
            }
        }

        private short back(int code) {
            int stepSize = STEPS[index];
            int delta = stepSize >> 3;

            if ((code & 4) != 0) delta += stepSize;
            if ((code & 2) != 0) delta += stepSize >> 1;
            if ((code & 1) != 0) delta += stepSize >> 2;

            predictor += (code & 8) != 0 ? -delta : delta;
            clamp();

            index += TABLE[code & 7];
            if (index < 0) index = 0;
            if (index > 88) index = 88;

            return (short) predictor;
        }

        private void clamp() {
            if (predictor > 32767) predictor = 32767;
            if (predictor < -32768) predictor = -32768;
        }
    }
}

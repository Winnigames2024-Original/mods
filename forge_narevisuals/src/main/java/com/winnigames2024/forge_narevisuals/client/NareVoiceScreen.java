package com.winnigames2024.forge_narevisuals.client;

import com.winnigames2024.forge_narevisuals.Config;
import net.minecraft.client.gui.GuiGraphics;
import net.minecraft.client.gui.screens.Screen;
import net.minecraft.network.chat.Component;

import java.util.ArrayList;
import java.util.List;

/**
 * Настройки голоса: режим, громкости, микрофон. Сверху живая полоска уровня —
 * сразу видно, слышит ли мод микрофон вообще, и не надо гадать.
 */
public class NareVoiceScreen extends Screen {

    private static final int ROW_H = 20;

    private static final String[] MODES = { "Держать клавишу", "Нажал — говоришь", "По голосу" };

    private final Screen parent;

    private int scroll;
    private int px, py, pw, ph;
    private Integer dragging;

    private List<String> mics = new ArrayList<>();

    private static final class Row {
        enum Kind { LABEL, TOGGLE, ACTION, SLIDER, MIC }

        final Kind kind;
        final String title;
        final String hint;

        java.util.function.BooleanSupplier state;
        Runnable act;

        java.util.function.IntSupplier value;
        java.util.function.IntConsumer set;
        java.util.function.IntFunction<String> text;
        int min, max;

        String mic;

        Row(Kind kind, String title, String hint) {
            this.kind = kind;
            this.title = title;
            this.hint = hint;
        }
    }

    public NareVoiceScreen(Screen parent) {
        super(Component.literal("Голос"));
        this.parent = parent;
    }

    @Override
    protected void init() {
        try { mics = NareVoice.microphones(); }
        catch (Exception e) { mics = new ArrayList<>(); }
    }

    // ------------------------------------------------------------- строки

    private List<Row> rows() {
        Config cfg = Config.get();
        var list = new ArrayList<Row>();

        Row on = new Row(Row.Kind.TOGGLE, "Голосовой чат", state());
        on.state = () -> cfg.voiceEnabled;
        on.act = () -> {
            cfg.voiceEnabled = !cfg.voiceEnabled;
            Config.save();
            if (cfg.voiceEnabled) NareVoice.start();
            else NareVoice.stop();
        };
        list.add(on);

        Row mic = new Row(Row.Kind.TOGGLE, "Мой микрофон",
                cfg.voiceMuteMic ? "выключен — вас не слышно" : "включён");
        mic.state = () -> !cfg.voiceMuteMic;
        mic.act = () -> {
            cfg.voiceMuteMic = !cfg.voiceMuteMic;
            Config.save();
        };
        list.add(mic);

        Row ears = new Row(Row.Kind.TOGGLE, "Слышать других",
                cfg.voiceMuteAll ? "выключено — тишина" : "включено");
        ears.state = () -> !cfg.voiceMuteAll;
        ears.act = () -> {
            cfg.voiceMuteAll = !cfg.voiceMuteAll;
            Config.save();
        };
        list.add(ears);

        Row mode = new Row(Row.Kind.ACTION, "Режим", MODES[clampMode(cfg.voiceMode)]);
        mode.act = () -> {
            cfg.voiceMode = (clampMode(cfg.voiceMode) + 1) % MODES.length;
            Config.save();
        };
        list.add(mode);

        list.add(slider("Громкость голосов", null, 0, 200,
                () -> cfg.voiceVolume, v -> { cfg.voiceVolume = v; Config.save(); },
                v -> v + "%"));

        list.add(slider("Усиление микрофона", "Если вас плохо слышно", 0, 300,
                () -> cfg.voiceMicGain, v -> { cfg.voiceMicGain = v; Config.save(); },
                v -> v + "%"));

        if (clampMode(cfg.voiceMode) == 2)
            list.add(slider("Порог", "Ниже него считается тишиной", 0, 100,
                    () -> cfg.voiceThreshold, v -> { cfg.voiceThreshold = v; Config.save(); },
                    v -> v + "%"));

        list.add(slider("Слышно за", distanceHint(), 8, 256,
                () -> cfg.voiceDistance,
                v -> {
                    cfg.voiceDistance = v;
                    cfg.voiceDistanceCustom = true;
                    Config.save();
                },
                v -> v + " бл."));

        if (cfg.voiceDistanceCustom && NareNet.voiceDistance() >= 0) {
            Row back = new Row(Row.Kind.ACTION, "Вернуть как на сервере", null);
            back.act = () -> {
                cfg.voiceDistanceCustom = false;
                Config.save();
            };
            list.add(back);
        }

        list.add(new Row(Row.Kind.LABEL, "Кто рядом", null));

        var people = NareVoice.known();
        if (people.isEmpty())
            list.add(new Row(Row.Kind.LABEL, "никого не слышно", null));

        for (String who : people) {
            boolean talking = NareVoice.talkers().contains(who);

            list.add(slider(who, talking ? "говорит" : null, 0, 200,
                    () -> NareVoice.personVolume(who),
                    v -> NareVoice.setPersonVolume(who, v),
                    v -> v == 0 ? "тихо" : v + "%"));
        }

        list.add(new Row(Row.Kind.LABEL, "Микрофон", null));

        Row auto = new Row(Row.Kind.MIC, "Как в системе", null);
        auto.mic = "";
        list.add(auto);

        for (String name : mics) {
            Row row = new Row(Row.Kind.MIC, name, null);
            row.mic = name;
            list.add(row);
        }

        if (mics.isEmpty())
            list.add(new Row(Row.Kind.LABEL, "Система не показала ни одного микрофона", null));

        return list;
    }

    /** Подсказка под ползунком дальности — что сейчас на самом деле. */
    private static String distanceHint() {
        int server = NareNet.voiceDistance();

        if (server < 0) return "сервер про дальность молчит";
        if (!Config.get().voiceDistanceCustom)
            return server == 0 ? "сейчас по-серверному: слышно везде"
                               : "сейчас по-серверному: " + server + " бл.";

        if (server == 0) return "на сервере слышно везде, вы убавили";
        if (Config.get().voiceDistance > server) return "сервер всё равно режет до " + server + " бл.";

        return "на сервере " + server + " бл.";
    }

    private static int clampMode(int value) {
        return value < 0 || value >= MODES.length ? 0 : value;
    }

    private String state() {
        if (!Config.get().voiceEnabled) return "выключен";

        String trouble = NareVoice.trouble();
        if (!trouble.isEmpty()) return "беда: " + trouble;

        if (!NareNet.plugged()) return "нужен плагин на сервере";
        if (!NareVoice.running()) return "не запущен";

        var talkers = NareVoice.talkers();
        return talkers.isEmpty() ? "работает" : "говорят: " + String.join(", ", talkers);
    }

    private static Row slider(String title, String hint, int min, int max,
                              java.util.function.IntSupplier value,
                              java.util.function.IntConsumer set,
                              java.util.function.IntFunction<String> text) {
        Row row = new Row(Row.Kind.SLIDER, title, hint);
        row.min = min;
        row.max = max;
        row.value = value;
        row.set = set;
        row.text = text;
        return row;
    }

    // ---------------------------------------------------------- отрисовка

    private void measure() {
        pw = Math.min(440, Math.max(220, this.width - 20));
        ph = Math.min(380, Math.max(140, this.height - 20));
        px = (this.width - pw) / 2;
        py = (this.height - ph) / 2;
    }

    private int visibleRows() {
        return Math.max(1, (ph - 68 - 26) / ROW_H);
    }

    @Override
    public void render(GuiGraphics g, int mouseX, int mouseY, float partialTick) {
        super.render(g, mouseX, mouseY, partialTick);
        measure();

        g.fill(0, 0, this.width, this.height, 0x99000000);

        NareTheme.roundRect(g, px, py, pw, ph, 8, 0xF40D1018);
        NareTheme.outline(g, px, py, pw, ph, 8, 0x2EFFFFFF);

        NareTheme.bold(g, font, "Голосовой чат", px + 14, py + 13, NareTheme.TEXT);

        // живая полоска: видно, доходит ли звук до мода
        int barX = px + 70;
        int barW = pw - 84;
        int barY = py + 32;

        g.fill(barX, barY, barX + barW, barY + 6, 0x22FFFFFF);

        float level = NareVoice.level();
        int fill = (int) (barW * Math.min(1f, level));
        if (fill > 0)
            g.fill(barX, barY, barX + fill, barY + 6,
                    NareVoice.sending() ? NareTheme.accent() : 0x66FFFFFF);

        if (clampMode(Config.get().voiceMode) == 2) {
            int mark = barX + (int) (barW * Config.get().voiceThreshold / 100f);
            g.fill(mark, barY - 2, mark + 1, barY + 8, NareTheme.WARN);
        }

        g.drawString(font, "микрофон", px + 14, barY - 1, NareTheme.DIM, false);

        var list = rows();
        int shown = visibleRows();
        if (scroll > Math.max(0, list.size() - shown)) scroll = Math.max(0, list.size() - shown);
        if (scroll < 0) scroll = 0;

        int cx = px + 10;
        int cw = pw - 20;
        int limit = Math.min(list.size() - scroll, shown);

        for (int i = 0; i < limit; i++) {
            Row row = list.get(i + scroll);
            int y = py + 68 + i * ROW_H;
            boolean hover = mouseX >= cx && mouseX <= cx + cw && mouseY >= y && mouseY < y + ROW_H - 2;

            if (row.kind == Row.Kind.LABEL) {
                NareTheme.bold(g, font, row.title.toUpperCase(), cx + 4, y + 6,
                        NareTheme.alpha(NareTheme.accent(), 0.85f));
                continue;
            }

            if (hover) NareTheme.roundRect(g, cx, y, cw, ROW_H - 2, 4, 0x12FFFFFF);

            int ty = row.hint == null ? y + 6 : y + 2;
            g.drawString(font, row.title, cx + 6, ty, 0xFFD2DAE8, false);
            if (row.hint != null)
                g.drawString(font, row.hint, cx + 6, y + 12, NareTheme.DIM, false);

            switch (row.kind) {
                case TOGGLE -> lamp(g, cx + cw - 20, y + 6, row.state.getAsBoolean());
                case MIC -> lamp(g, cx + cw - 20, y + 6,
                        row.mic.equals(Config.get().voiceDevice));
                case SLIDER -> slider(g, row, cx, cw, y);
                case ACTION -> g.drawString(font, "›", cx + cw - 14, y + 6,
                        hover ? NareTheme.accent() : NareTheme.DIM, false);
                default -> { }
            }
        }

        String talk = NareKeys.TALK.getTranslatedKeyMessage().getString();
        g.drawString(font, "Говорить — " + talk + " · Esc — назад",
                px + 14, py + ph - 16, NareTheme.DIM, false);
    }

    private static void lamp(GuiGraphics g, int x, int y, boolean on) {
        int accent = NareTheme.accent();
        if (on) {
            NareTheme.roundRect(g, x, y, 8, 8, 2, accent);
            NareTheme.roundRect(g, x - 1, y - 1, 10, 10, 3, NareTheme.alpha(accent, 0.25f));
        }
        else NareTheme.outline(g, x, y, 8, 8, 2, 0x55FFFFFF);
    }

    private void slider(GuiGraphics g, Row row, int cx, int cw, int y) {
        int barW = 86;
        int x = cx + cw - barW - 6;
        int barY = y + 8;

        int value = row.value.getAsInt();
        float k = row.max == row.min ? 0f : (float) (value - row.min) / (row.max - row.min);

        g.fill(x, barY, x + barW, barY + 2, 0x33FFFFFF);
        g.fill(x, barY, x + (int) (barW * k), barY + 2, NareTheme.accent());

        int knob = x + (int) (barW * k);
        NareTheme.roundRect(g, knob - 2, barY - 3, 4, 8, 2, NareTheme.accent());

        String text = row.text == null ? String.valueOf(value) : row.text.apply(value);
        g.drawString(font, text, x - font.width(text) - 8, y + 6, NareTheme.MUTED, false);
    }

    // --------------------------------------------------------------- мышь

    @Override
    public boolean mouseClicked(double mouseX, double mouseY, int button) {
        measure();

        var list = rows();
        int limit = Math.min(list.size() - scroll, visibleRows());

        int cx = px + 10;
        int cw = pw - 20;

        for (int i = 0; i < limit; i++) {
            int y = py + 68 + i * ROW_H;
            if (mouseX < cx || mouseX > cx + cw) continue;
            if (mouseY < y || mouseY >= y + ROW_H - 2) continue;

            Row row = list.get(i + scroll);

            switch (row.kind) {
                case TOGGLE, ACTION -> { if (row.act != null) row.act.run(); }
                case MIC -> {
                    Config.get().voiceDevice = row.mic;
                    Config.save();

                    // микрофон меняется только на перезапуске захвата
                    NareVoice.stop();
                    NareVoice.start();
                }
                case SLIDER -> {
                    dragging = i + scroll;
                    apply(row, mouseX, cx, cw);
                }
                default -> { }
            }
            return true;
        }

        return super.mouseClicked(mouseX, mouseY, button);
    }

    @Override
    public boolean mouseDragged(double mouseX, double mouseY, int button, double dx, double dy) {
        if (dragging != null) {
            var list = rows();
            if (dragging >= 0 && dragging < list.size())
                apply(list.get(dragging), mouseX, px + 10, pw - 20);
            return true;
        }
        return super.mouseDragged(mouseX, mouseY, button, dx, dy);
    }

    @Override
    public boolean mouseReleased(double mouseX, double mouseY, int button) {
        dragging = null;
        return super.mouseReleased(mouseX, mouseY, button);
    }

    private void apply(Row row, double mouseX, int cx, int cw) {
        if (row.kind != Row.Kind.SLIDER) return;

        int barW = 86;
        int x = cx + cw - barW - 6;

        float k = (float) ((mouseX - x) / barW);
        if (k < 0f) k = 0f;
        if (k > 1f) k = 1f;

        row.set.accept(row.min + Math.round(k * (row.max - row.min)));
    }

    @Override
    public boolean mouseScrolled(double mouseX, double mouseY, double sx, double sy) {
        if (sy != 0) scroll -= (int) Math.signum(sy);
        return true;
    }

    @Override
    public void onClose() {
        Config.save();
        minecraft.setScreen(parent);
    }

    @Override
    public boolean isPauseScreen() {
        return false;
    }
}

using Verse;

namespace Ustas.RimAI.Communication.Voices.Data
{
    public static class TTSConstant
    {
        public static readonly string Lang = LanguageDatabase.activeLanguage.info.friendlyNameNative;

        public static readonly string DefaultTTSProcessingPrompt =
            """
            Ти професійно готуєш текст для синтезу мовлення (TTS).

            Правила:
            1. Переклади весь текст мовою {language}.
            2. У дужках перекладай лише вміст, зберігай дужки й не додавай позначок.
            3. Поза дужками перекладай текст і додавай доречні позначки з наведеного нижче списку.
            - На початку кожного речення став одну емоцію, відокремлену пробілом.
            - Позначки тону й звукові ефекти можна ставити будь-де в реченні.
            - Замінюй трикрапки (...) на [break] або [long-break], після чого прибирай трикрапки.
            - Після кожного речення поза дужками додавай [break].
            4. Ніколи не додавай позначок усередині дужок.
            5. Виведи лише JSON:
            {
                "text": "<повністю перекладений мовою {language} та розмічений текст зі збереженими дужками й перекладеним вмістом>",
                "emotion": "<порожній рядок>"
            }

            Доступні позначки:
            Емоції: [happy], [sad], [angry], [excited], [calm], [nervous], [confident], [surprised], [satisfied], [delighted], [scared], [worried], [upset], [frustrated], [depressed], [empathetic], [embarrassed], [disgusted], [moved], [proud], [relaxed], [grateful], [curious], [sarcastic], [disdainful], [unhappy], [anxious], [hysterical], [indifferent], [uncertain], [doubtful], [confused], [disappointed], [regretful], [guilty], [ashamed], [jealous], [envious], [hopeful], [optimistic], [pessimistic], [nostalgic], [lonely], [bored], [contemptuous], [sympathetic], [compassionate], [determined], [resigned]
            Тон: [in a hurry tone], [shouting], [screaming], [whispering], [soft tone]
            Звукові ефекти: [laughing], [chuckling], [sobbing], [crying loudly], [sighing], [groaning], [panting], [gasping], [yawning], [snoring]
            Паузи: [break], [long-break]
            """;

        public static readonly string DefaultTTSProcessingPrompt_CosyVoice =
            """
            Ти професійно готуєш текст для синтезу мовлення (TTS).

            Правила:
            1. Переклади весь текст мовою {language}.
            2. У дужках перекладай лише вміст, зберігай дужки й не додавай позначок.
            3. Поза дужками перекладай текст і додавай доречні позначки з наведеного нижче списку.
            4. Не додавай позначок усередині дужок.
            5. Виведи лише JSON:
            {
                "text": "<повністю перекладений мовою {language} і розмічений текст зі збереженими дужками та їхнім перекладеним вмістом>",
                "emotion": "<найдоречніша емоція>"
            }

            Доступні позначки:
            Емоція (поле emotion, вибери одну): Happy, Sad, Angry, Excited, Calm, Fearful, Disgusted, Confused
            Тон і звукові ефекти (можна додавати в полі text поза дужками): [breath], <strong></strong>, [noise], [laughter], [cough], [clucking], [accent], [quick_breath], <laughter></laughter>, [hissing], [sigh], [vocalized-noise], [lipsmack]
            """;

        public static readonly string DefaultTTSProcessingPrompt_IndexTTS =
            """
            Ти професійний перекладач.

            Правила:
            1. Переклади весь текст мовою {language}.
            2. У дужках перекладай лише вміст і зберігай дужки.
            3. Поза дужками перекладай текст мовою {language}.
            4. Виведи лише JSON:
            {
                "text": "<повністю перекладений мовою {language} текст зі збереженими дужками та їхнім перекладеним вмістом>",
                "emotion": "<порожній рядок>"
            }
            """;

        public static readonly string DefaultTTSProcessingPrompt_AzureTTS =
            """
            Ти професійно готуєш текст для Microsoft Azure Text-to-Speech.

            Правила:
            1. Переклади весь текст мовою {language}.
            2. У дужках перекладай лише вміст, зберігай дужки й не додавай розмітки.
            3. Поза дужками перекладай текст і додавай сумісну з Azure TTS розмітку SSML.
            4. Ніколи не додавай теги всередині дужок.
            5. Виведи лише JSON:
            {
                "text": "<повністю перекладений мовою {language} текст із тегами SSML і збереженими дужками>",
                "emotion": "<найдоречніший стиль мовлення зі списку нижче або порожній рядок>"
            }

            Доступні стилі Azure TTS для поля emotion (вибери один або залиш порожнім):
            cheerful, sad, angry, excited, friendly, terrified, shouting, unfriendly, whispering, hopeful, calm, fearful, embarrassed, serious, depressed, disgruntled, assistant, newscast, customerservice.

            Доступна розмітка SSML у полі text поза дужками:
            - [break], [break:500ms], [long-break], [break:1s], [break:2s] — паузи.
            - [emphasis]word[/emphasis], [emphasis:strong]IMPORTANT[/emphasis], [emphasis:reduced]minor[/emphasis] — наголос.
            - [telephone]555-0123[/telephone] — вимова номера телефону.
            - [date]2024-01-13[/date] — природна вимова дати.
            """;

        public static readonly string DefaultTTSProcessingPrompt_EdgeTTS =
            """
            Ти професійно готуєш текст для Microsoft Edge-TTS.

            Правила:
            1. Переклади весь текст мовою {language}.
            2. Виведи лише JSON:
            {
                "text": "<повністю перекладений мовою {language} текст зі збереженими дужками та їхнім перекладеним вмістом>",
                "emotion": "<порожній рядок>"
            }
            """;

        public static readonly string DefaultTTSProcessingPrompt_GeminiTTS =
            """
            Ти професійно готуєш текст для Google Gemini Text-to-Speech.

            Правила:
            1. Переклади весь текст мовою {language}.
            2. У дужках перекладай лише вміст, зберігай дужки й не додавай вказівок.
            3. Поза дужками перекладай текст і додавай природномовні вказівки щодо стилю.
            4. Ніколи не додавай вказівок усередині дужок.
            5. Виведи лише JSON:
            {
                "text": "<повністю перекладений мовою {language} текст зі стильовими вказівками та збереженими дужками>",
                "emotion": "<порожній рядок>"
            }

            Керування стилем природною мовою:
            Gemini TTS сприймає вказівки на початку тексту або перед окремими частинами. Можна визначати емоцію, тон, манеру, темп, акцент чи характер голосу та застосовувати різні стилі до різних частин одного тексту. Будь творчим і конкретним, не змінюючи змісту.
            Доступні голоси: Kore, Puck, Aoede, Enceladus, Charon, Fenrir, Leda, Callirrhoe та інші.
            """;

        public static readonly string DefaultTTSProcessingPrompt_OpenAI =
            """
            Ти професійно готуєш текст для OpenAI Text-to-Speech.

            Правила:
            1. Переклади весь текст мовою {language}.
            2. У дужках перекладай лише вміст і зберігай дужки.
            3. Не додавай жодних технічних позначок чи розмітки: модель озвучує текст буквально, а стиль задають окремі інструкції.
            4. Замінюй трикрапки на природну пунктуацію, щоб пауза звучала природно.
            5. Виведи лише JSON:
            {
                "text": "<повністю перекладений мовою {language} текст зі збереженими дужками та їхнім перекладеним вмістом>",
                "emotion": "<порожній рядок>"
            }
            """;

        public static readonly string DefaultTTSProcessingPrompt_TTSWebUI =
            """
            Ти професійно готуєш текст для TTS-WebUI.

            Правила:
            1. Переклади весь текст мовою {language}.
            2. У дужках перекладай лише вміст і зберігай дужки.
            3. Поза дужками перекладай текст і за потреби додавай природні паузи.
            4. Використовуй "..." для природних пауз між реченнями або думками.
            5. Виведи лише JSON:
            {
                "text": "<повністю перекладений мовою {language} текст зі збереженими дужками та їхнім перекладеним вмістом>",
                "emotion": "<порожній рядок>"
            }
            """;

        public static string GetTTSProcessingPrompt(TTSSettings settings)
        {
            if (settings == null)
                return DefaultTTSProcessingPrompt;

            return string.IsNullOrWhiteSpace(settings.CustomTTSProcessingPrompt)
                ? DefaultTTSProcessingPrompt
                : settings.CustomTTSProcessingPrompt;
        }
    }
}

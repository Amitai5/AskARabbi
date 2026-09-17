namespace AskARabbi.Api.Conversations;

/// <summary>Provides an honest saved reply when no generated answer can be validated.</summary>
internal static class ConversationRecoveryText
{
    internal static string ForLanguage(string? language) => language?.Trim().ToLowerInvariant() switch
    {
        "french" or "fr" => "Je ne peux pas encore répondre à cette question avec assez de certitude. Je préfère reconnaître cette limite plutôt que deviner. Pouvez-vous préciser le point qui vous intéresse le plus ou le passage auquel vous pensez ?",
        "german" or "de" => "Diese Frage kann ich noch nicht zuverlässig beantworten. Ich möchte diese Grenze offen benennen, statt zu raten. Welcher Punkt ist Ihnen am wichtigsten, oder welche Textstelle meinen Sie?",
        "hebrew" or "he" => "עדיין אין לי תשובה מספיק מבוססת לשאלה הזאת. אני מעדיף לומר זאת בגלוי ולא לנחש. באיזו נקודה תרצו להתמקד, או לאיזה קטע התכוונתם?",
        "italian" or "it" => "Non riesco ancora a rispondere a questa domanda con sufficiente certezza. Preferisco riconoscere questo limite anziché indovinare. Quale punto ti interessa di più, o a quale passo ti riferisci?",
        "persian" or "fa" => "هنوز نمی‌توانم با اطمینان کافی به این پرسش پاسخ بدهم. ترجیح می‌دهم این محدودیت را روشن بگویم تا اینکه حدس بزنم. کدام نکته برایتان مهم‌تر است، یا منظورتان کدام بخش از متن است؟",
        "polish" or "pl" => "Nie potrafię jeszcze odpowiedzieć na to pytanie z wystarczającą pewnością. Wolę otwarcie to przyznać, niż zgadywać. Który punkt interesuje Cię najbardziej lub o jaki fragment chodzi?",
        "russian" or "ru" => "Пока я не могу достаточно уверенно ответить на этот вопрос. Лучше честно признать это, чем гадать. Какой момент вас интересует больше всего или какой отрывок вы имеете в виду?",
        "spanish" or "es" => "Todavía no puedo responder a esa pregunta con suficiente certeza. Prefiero reconocer ese límite antes que adivinar. ¿Qué punto te interesa más, o a qué pasaje te refieres?",
        "yiddish" or "yi" => "איך קען נאָך נישט ענטפֿערן אויף דער פֿראַגע מיט גענוג זיכערקייט. איך וויל דאָס זאָגן אָפֿן און נישט טרעפֿן. וועלכער פּונקט אינטערעסירט אײַך מערסט, אָדער וועלכן טעקסט מיינט איר?",
        _ => "I'm not confident enough to give a reliable answer to that yet. I'd rather be clear about that than guess. Which part would you like to focus on, or is there a particular passage you have in mind?",
    };
}

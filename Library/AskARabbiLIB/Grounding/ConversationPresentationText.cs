namespace AskARabbiLIB.Grounding;

/// <summary>Keeps application-written answer transitions in the chosen conversation language.</summary>
internal sealed record ConversationPresentationText(string Perspective, string Continuation, string Guidance, string QuotationFallback)
{
    internal static ConversationPresentationText ForLanguage(string? language) => ConversationPersonalization.NormalizeLanguage(language) switch
    {
        "French" => new("Une autre perspective :", "Pour poursuivre :", "Pour une décision pratique qui dépend de votre situation, consultez un rabbin qualifié qui la connaît.", "Certaines citations ne sont pas disponibles ici dans la langue choisie ({0}) ; leur texte approuvé est conservé en {1}."),
        "German" => new("Eine andere Perspektive:", "Zum Weiterdenken:", "Besprechen Sie eine praktische Entscheidung, die von Ihrer Situation abhängt, mit einem qualifizierten Rabbiner, der diese kennt.", "Einige Zitate sind hier nicht in der gewählten Sprache ({0}) verfügbar; der geprüfte Wortlaut bleibt in {1} erhalten."),
        "Hebrew" => new("נקודת מבט נוספת:", "להמשך הלימוד:", "בשאלה מעשית התלויה בנסיבות האישיות, כדאי להתייעץ עם רב מוסמך שמכיר את המצב.", "חלק מהציטוטים אינם זמינים כאן בשפה שנבחרה ({0}); נוסח המקור המאושר נשמר בשפה {1}."),
        "Italian" => new("Un'altra prospettiva:", "Per approfondire:", "Per una decisione pratica che dipende dalle tue circostanze, consulta un rabbino qualificato che conosca la situazione.", "Alcune citazioni non sono disponibili qui nella lingua scelta ({0}); il testo approvato resta in {1}."),
        "Persian" => new("دیدگاهی دیگر:", "برای ادامهٔ مطالعه:", "برای تصمیمی عملی که به شرایط شخصی شما بستگی دارد، با خاخامی آگاه و آشنا با وضعیت خود مشورت کنید.", "برخی نقل‌قول‌ها در اینجا به زبان انتخاب‌شده ({0}) در دسترس نیستند؛ متن تأییدشده به زبان {1} حفظ شده است."),
        "Polish" => new("Inna perspektywa:", "Aby zgłębić temat:", "Decyzję praktyczną zależną od Twojej sytuacji warto omówić z wykwalifikowanym rabinem, który zna te okoliczności.", "Niektóre cytaty nie są tutaj dostępne w wybranym języku ({0}); zachowano zatwierdzony tekst w języku {1}."),
        "Russian" => new("Другая точка зрения:", "Для дальнейшего изучения:", "Практический вопрос, зависящий от ваших обстоятельств, стоит обсудить с квалифицированным раввином, знакомым с вашей ситуацией.", "Некоторые цитаты здесь недоступны на выбранном языке ({0}); сохранён утверждённый текст на языке {1}."),
        "Spanish" => new("Otra perspectiva:", "Para seguir explorando:", "Para una decisión práctica que dependa de tus circunstancias, consulta a un rabino cualificado que conozca tu situación.", "Algunas citas no están disponibles aquí en el idioma elegido ({0}); se conserva el texto aprobado en {1}."),
        "Yiddish" => new("אַן אַנדער בליקווינקל:", "צו לערנען ווײַטער:", "פֿאַר אַ פּראַקטישער פֿראַגע וואָס איז אָפּהענגיק פֿון אײַערע אומשטענדן, רעדט מיט אַ באַפֿוגטן רב וואָס קען אײַער מצב.", "עטלעכע ציטאַטן זענען דאָ נישט פֿאַראַן אין דער אויסגעקליבענער שפּראַך ({0}); דער באַשטעטיקטער טעקסט בלײַבט אין {1}."),
        _ => new("Another perspective:", "If you'd like to keep exploring:", "Because the practical answer may depend on your circumstances, talk it through with a qualified rabbi who knows your situation.", "Some quotations are not available here in your selected language ({0}); their approved wording is retained in {1}."),
    };
}

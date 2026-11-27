namespace PermisVR.Route
{
    /// French wording of each examiner phrase: the subtitle, and the text the voice clips are generated from.
    public static class ExaminerPhrases
    {
        public static string Text(ExaminerPhrase phrase)
        {
            switch (phrase)
            {
                case ExaminerPhrase.Depart:
                    return "Bonjour. Nous allons commencer. Quand vous êtes prêt, démarrez et suivez la route. Je vous indiquerai la direction.";
                case ExaminerPhrase.NextLeft:
                    return "Prenez la prochaine à gauche.";
                case ExaminerPhrase.NextRight:
                    return "Prenez la prochaine à droite.";
                case ExaminerPhrase.SecondLeft:
                    return "Prenez la deuxième à gauche.";
                case ExaminerPhrase.SecondRight:
                    return "Prenez la deuxième à droite.";
                case ExaminerPhrase.StraightOn:
                    return "Au prochain carrefour, continuez tout droit.";
                case ExaminerPhrase.RoundaboutExit1:
                    return "Au rond-point, prenez la première sortie.";
                case ExaminerPhrase.RoundaboutExit2:
                    return "Au rond-point, prenez la deuxième sortie.";
                case ExaminerPhrase.RoundaboutExit3:
                    return "Au rond-point, prenez la troisième sortie.";
                case ExaminerPhrase.RoundaboutExit4:
                    return "Au rond-point, prenez la quatrième sortie.";
                case ExaminerPhrase.PullOver:
                    return "Garez-vous sur la droite dès que possible, en toute sécurité.";
                case ExaminerPhrase.ExamFinished:
                    return "Merci, l'examen est terminé. Vous pouvez serrer le frein à main.";
                case ExaminerPhrase.OffRoute:
                    return "Ce n'est pas grave. Faites demi-tour dès que possible pour reprendre l'itinéraire.";
                default:
                    return "";
            }
        }
    }
}

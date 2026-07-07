using LostieLauncher.Models;

namespace LostieLauncher.Content;

public readonly record struct FaqEntry(string Question, string Answer);

public interface IFaqs
{
    public IReadOnlyList<FaqEntry> Entries { get; }
}

public static class Faqs
{
    public static IFaqs For(AppLanguage language) => language switch
    {
        AppLanguage.Eng => new EngFaqs(),
        AppLanguage.Cat => new CatFaqs(),
        AppLanguage.Eus => new EusFaqs(),
        AppLanguage.Gal => new GalFaqs(),
        AppLanguage.Por => new PorFaqs(),
        AppLanguage.Val => new ValFaqs(),
        AppLanguage.Fra => new FraFaqs(),
        _ => new EspFaqs()
    };
}

public class EspFaqs : IFaqs
{
    public IReadOnlyList<FaqEntry> Entries =>
    [
        new("¿Cómo descargo un juego?",
            "Ve a la Biblioteca, elige el juego que quieras y pulsa Descargar. Cuando termine la instalación, aparecerá en Mis Juegos listo para jugar."),
        new("¿Dónde se instalan los juegos y cómo cambio la carpeta?",
            "Los juegos se instalan en el directorio de descargas configurado. Puedes cambiarlo en Ajustes, en la opción Directorio de descargas. Si ya tienes juegos instalados, tendrás que moverlos manualmente a la nueva ruta."),
        new("¿Perderé mis partidas guardadas al actualizar o desinstalar un juego?",
            "No. Las partidas guardadas y el registro de tiempo jugado se conservan siempre, tanto al actualizar como al desinstalar un juego."),
        new("¿Qué es una versión especial y cómo la activo?",
            "Es una versión alternativa de un juego que se desbloquea con una clave con formato XXXX-XXXX-XXXX-XXXX-XXXX. Puedes introducir la clave al descargar el juego o cambiar a la versión especial desde Mis Juegos sin perder nada."),
        new("¿Qué significa el modo offline?",
            "Significa que el launcher no puede conectar con el servidor, ya sea porque no tienes conexión a internet o porque el servidor está en mantenimiento. Puedes seguir jugando a tus juegos instalados; las descargas, actualizaciones y versiones especiales se reactivarán automáticamente cuando vuelva la conexión."),
        new("He encontrado un error, ¿dónde lo reporto?",
            "Escribe en el canal #testeo-launcher del Discord de la comunidad contando qué ha pasado y qué estabas haciendo. Cuanto más detalle des, más fácil será arreglarlo.")
    ];
}

public class EngFaqs : IFaqs
{
    public IReadOnlyList<FaqEntry> Entries =>
    [
        new("How do I download a game?",
            "Go to the Library, pick the game you want and press Download. Once the installation finishes, it will appear in My Games ready to play."),
        new("Where are games installed and how do I change the folder?",
            "Games are installed in the configured download directory. You can change it in Settings, under Download directory. If you already have games installed, you will need to move them manually to the new path."),
        new("Will I lose my saved games when updating or uninstalling a game?",
            "No. Saved games and the playtime record are always kept, both when updating and when uninstalling a game."),
        new("What is a special version and how do I activate it?",
            "It is an alternative version of a game unlocked with a key in the format XXXX-XXXX-XXXX-XXXX-XXXX. You can enter the key when downloading the game or switch to the special version from My Games without losing anything."),
        new("What does offline mode mean?",
            "It means the launcher can't reach the server, either because you have no internet connection or because the server is under maintenance. You can keep playing your installed games; downloads, updates and special versions will reactivate automatically when the connection returns."),
        new("I found a bug, where do I report it?",
            "Write in the #testeo-launcher channel of the community Discord explaining what happened and what you were doing. The more detail you give, the easier it will be to fix.")
    ];
}

public class CatFaqs : IFaqs
{
    public IReadOnlyList<FaqEntry> Entries =>
    [
        new("Com descarrego un joc?",
            "Ves a la Biblioteca, tria el joc que vulguis i prem Descarregar. Quan acabi la instal·lació, apareixerà a Els meus jocs a punt per jugar."),
        new("On s'instal·len els jocs i com canvio la carpeta?",
            "Els jocs s'instal·len al directori de descàrregues configurat. Pots canviar-lo a Configuració, a l'opció Directori de descàrregues. Si ja tens jocs instal·lats, hauràs de moure'ls manualment a la nova ruta."),
        new("Perdré les meves partides desades en actualitzar o desinstal·lar un joc?",
            "No. Les partides desades i el registre de temps jugat es conserven sempre, tant en actualitzar com en desinstal·lar un joc."),
        new("Què és una versió especial i com l'activo?",
            "És una versió alternativa d'un joc que es desbloqueja amb una clau amb format XXXX-XXXX-XXXX-XXXX-XXXX. Pots introduir la clau en descarregar el joc o canviar a la versió especial des d'Els meus jocs sense perdre res."),
        new("Què significa el mode offline?",
            "Significa que el launcher no pot connectar amb el servidor, ja sigui perquè no tens connexió a internet o perquè el servidor està en manteniment. Pots seguir jugant als teus jocs instal·lats; les descàrregues, actualitzacions i versions especials es reactivaran automàticament quan torni la connexió."),
        new("He trobat un error, on el reporto?",
            "Escriu al canal #testeo-launcher del Discord de la comunitat explicant què ha passat i què estaves fent. Com més detall donis, més fàcil serà arreglar-ho.")
    ];
}

public class EusFaqs : IFaqs
{
    public IReadOnlyList<FaqEntry> Entries =>
    [
        new("Nola deskargatzen dut joko bat?",
            "Joan Liburutegira, aukeratu nahi duzun jokoa eta sakatu Deskargatu. Instalazioa amaitzean, Nire Jokoak atalean agertuko da jolasteko prest."),
        new("Non instalatzen dira jokoak eta nola aldatzen dut karpeta?",
            "Jokoak konfiguratutako deskarga direktorioan instalatzen dira. Ezarpenetan alda dezakezu, Deskarga direktorioa aukeran. Dagoeneko jokoak instalatuta badituzu, eskuz mugitu beharko dituzu bide berrira."),
        new("Gordetako partidak galduko ditut joko bat eguneratzean edo desinstalatzean?",
            "Ez. Gordetako partidak eta jolasdenboraren erregistroa beti mantentzen dira, bai eguneratzean bai desinstalatzean."),
        new("Zer da bertsio berezi bat eta nola aktibatzen dut?",
            "Jokoaren bertsio alternatibo bat da, XXXX-XXXX-XXXX-XXXX-XXXX formatuko gako batekin desblokeatzen dena. Gakoa jokoa deskargatzean sar dezakezu, edo bertsio berezira aldatu Nire Jokoak ataletik ezer galdu gabe."),
        new("Zer esan nahi du offline moduak?",
            "Launcher-a zerbitzariarekin konektatu ezin dela esan nahi du, interneteko konexiorik ez duzulako edo zerbitzaria mantentze-lanetan dagoelako. Instalatutako jokoetan jolasten jarrai dezakezu; deskargak, eguneraketak eta bertsio bereziak automatikoki berraktibatuko dira konexioa itzultzean."),
        new("Errore bat aurkitu dut, non jakinarazten dut?",
            "Idatzi komunitatearen Discord-eko #testeo-launcher kanalean, zer gertatu den eta zer egiten ari zinen azalduz. Zenbat eta xehetasun gehiago eman, orduan eta errazagoa izango da konpontzea.")
    ];
}

public class GalFaqs : IFaqs
{
    public IReadOnlyList<FaqEntry> Entries =>
    [
        new("Como descargo un xogo?",
            "Vai á Biblioteca, escolle o xogo que queiras e preme Descargar. Cando remate a instalación, aparecerá en Os meus xogos listo para xogar."),
        new("Onde se instalan os xogos e como cambio o cartafol?",
            "Os xogos instálanse no directorio de descargas configurado. Podes cambialo en Axustes, na opción Directorio de descargas. Se xa tes xogos instalados, terás que movelos manualmente á nova ruta."),
        new("Perderei as miñas partidas gardadas ao actualizar ou desinstalar un xogo?",
            "Non. As partidas gardadas e o rexistro de tempo xogado consérvanse sempre, tanto ao actualizar como ao desinstalar un xogo."),
        new("Que é unha versión especial e como a activo?",
            "É unha versión alternativa dun xogo que se desbloquea cunha clave co formato XXXX-XXXX-XXXX-XXXX-XXXX. Podes introducir a clave ao descargar o xogo ou cambiar á versión especial desde Os meus xogos sen perder nada."),
        new("Que significa o modo offline?",
            "Significa que o launcher non pode conectar co servidor, xa sexa porque non tes conexión a internet ou porque o servidor está en mantemento. Podes seguir xogando aos teus xogos instalados; as descargas, actualizacións e versións especiais reactivaranse automaticamente cando volva a conexión."),
        new("Atopei un erro, onde o reporto?",
            "Escribe na canle #testeo-launcher do Discord da comunidade contando que pasou e que estabas a facer. Canto máis detalle deas, máis fácil será arranxalo.")
    ];
}

public class PorFaqs : IFaqs
{
    public IReadOnlyList<FaqEntry> Entries =>
    [
        new("Como baixo um jogo?",
            "Vá à Biblioteca, escolha o jogo que deseja e pressione Baixar. Quando a instalação terminar, ele aparecerá em Meus Jogos pronto para jogar."),
        new("Onde os jogos são instalados e como mudo a pasta?",
            "Os jogos são instalados no diretório de downloads configurado. Você pode alterá-lo em Configurações, na opção Diretório de downloads. Se você já tiver jogos instalados, precisará movê-los manualmente para o novo caminho."),
        new("Vou perder meus saves ao atualizar ou desinstalar um jogo?",
            "Não. Os saves e o registro de tempo de jogo são sempre mantidos, tanto ao atualizar quanto ao desinstalar um jogo."),
        new("O que é uma versão especial e como a ativo?",
            "É uma versão alternativa de um jogo desbloqueada com uma chave no formato XXXX-XXXX-XXXX-XXXX-XXXX. Você pode inserir a chave ao baixar o jogo ou mudar para a versão especial em Meus Jogos sem perder nada."),
        new("O que significa o modo offline?",
            "Significa que o launcher não consegue se conectar ao servidor, seja porque você está sem conexão com a internet ou porque o servidor está em manutenção. Você pode continuar jogando seus jogos instalados; downloads, atualizações e versões especiais serão reativados automaticamente quando a conexão voltar."),
        new("Encontrei um erro, onde o reporto?",
            "Escreva no canal #testeo-launcher do Discord da comunidade contando o que aconteceu e o que você estava fazendo. Quanto mais detalhes você der, mais fácil será corrigir.")
    ];
}

public class ValFaqs : IFaqs
{
    public IReadOnlyList<FaqEntry> Entries =>
    [
        new("Com descarregue un joc?",
            "Ves a la Biblioteca, tria el joc que vulgues i prem Descarregar. Quan acabe la instal·lació, apareixerà a Els meus jocs a punt per a jugar."),
        new("On s'instal·len els jocs i com canvie la carpeta?",
            "Els jocs s'instal·len al directori de descàrregues configurat. Pots canviar-lo a Ajustos, a l'opció Directori de descàrregues. Si ja tens jocs instal·lats, hauràs de moure'ls manualment a la nova ruta."),
        new("Perdré les meues partides guardades en actualitzar o desinstal·lar un joc?",
            "No. Les partides guardades i el registre de temps jugat es conserven sempre, tant en actualitzar com en desinstal·lar un joc."),
        new("Què és una versió especial i com l'active?",
            "És una versió alternativa d'un joc que es desbloqueja amb una clau amb format XXXX-XXXX-XXXX-XXXX-XXXX. Pots introduir la clau en descarregar el joc o canviar a la versió especial des d'Els meus jocs sense perdre res."),
        new("Què significa el mode offline?",
            "Significa que el launcher no pot connectar amb el servidor, ja siga perquè no tens connexió a internet o perquè el servidor està en manteniment. Pots continuar jugant als teus jocs instal·lats; les descàrregues, actualitzacions i versions especials es reactivaran automàticament quan torne la connexió."),
        new("He trobat un error, on el reporte?",
            "Escriu al canal #testeo-launcher del Discord de la comunitat explicant què ha passat i què estaves fent. Com més detall dones, més fàcil serà arreglar-ho.")
    ];
}

public class FraFaqs : IFaqs
{
    public IReadOnlyList<FaqEntry> Entries =>
    [
        new("Comment télécharger un jeu ?",
            "Allez dans la Bibliothèque, choisissez le jeu souhaité et appuyez sur Télécharger. Une fois l'installation terminée, il apparaîtra dans Mes jeux, prêt à jouer."),
        new("Où les jeux sont-ils installés et comment changer de dossier ?",
            "Les jeux sont installés dans le répertoire de téléchargement configuré. Vous pouvez le modifier dans Paramètres, sous Répertoire de téléchargement. Si vous avez déjà des jeux installés, vous devrez les déplacer manuellement vers le nouveau chemin."),
        new("Vais-je perdre mes sauvegardes en mettant à jour ou en désinstallant un jeu ?",
            "Non. Les sauvegardes et le registre de temps de jeu sont toujours conservés, aussi bien lors d'une mise à jour que d'une désinstallation."),
        new("Qu'est-ce qu'une version spéciale et comment l'activer ?",
            "C'est une version alternative d'un jeu qui se débloque avec une clé au format XXXX-XXXX-XXXX-XXXX-XXXX. Vous pouvez saisir la clé lors du téléchargement du jeu ou passer à la version spéciale depuis Mes jeux sans rien perdre."),
        new("Que signifie le mode hors ligne ?",
            "Cela signifie que le launcher ne peut pas se connecter au serveur, soit parce que vous n'avez pas de connexion internet, soit parce que le serveur est en maintenance. Vous pouvez continuer à jouer à vos jeux installés ; les téléchargements, mises à jour et versions spéciales se réactiveront automatiquement au retour de la connexion."),
        new("J'ai trouvé un bug, où le signaler ?",
            "Écrivez dans le canal #testeo-launcher du Discord de la communauté en expliquant ce qui s'est passé et ce que vous faisiez. Plus vous donnez de détails, plus il sera facile de le corriger.")
    ];
}

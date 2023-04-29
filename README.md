<h1 align="center">ScreenShotGenerator</h1>

<h2 align="center">Сервис создания скриншотов сайтов.</h2>
<p align="left">
Цель данного проекта-возможность использования Selenium в круглосуточном режиме и при длительной нагрузке(min 50 000 отпечатков в сутки) разным контентом.
Вывод автора-нет не возможно использование.
</p>
<p align="left">
С 01.04.2023 ведется работа по рефакторингу кода и создание упрощенной версии. Все предыдущие коммиты удалены.
</p>
<p align="left">
Можно использовать kubernetes для мониторинга контейнеров, внутри которых работает  Selenium. Но такая идея не прорабатывалась.
</p>
<p align="left">
<a href="./Doc/UI/UI.md">Внешний вид UI</a> 
</p>
<p align="left">
<a href="./Doc/Api/Api.md">Описание API</a> 
</p>
<p align="left">
<a href="./Doc/TestMethod/TestMethod.md">Методика тестирования</a> 
</p>
<br>
<p align="left">
<h2>Описание</h2> 
</p>
<p align="left">
Создание скринов выполняет браузер Mozilla Firefox, управляемый Selenium. Для ускорения работы каждый браузер работает в отдельном потоке и получает информацию о задачах из пула задач.
Среднее время выполнение скриншотта на медленном соединении(100Мбит)-3.5c, на быстром соединении(1Гбит) может достигать 1с.
</p>
<p align="left">
При разработке сервиса возник ряд проблемм с браузерами.<br>
Браузер Chrome 95 версии(а так же Edge), теряет связь с драйвером после обработки некоторых сайтов. Причина не установлена. Больше не используется.<br>
Браузер Firefox постепенно заполняет весь swap системы. Это приводит к остановке сервиса.<br>
Заполнение swap происходит из-за открытия вкладок(или то что браузер счетает ими). Визуально(если сделать скрин шот) новых вкладок почти ни когда не видно.<br>
Дополнения ublock,pupup blocked unlimite,addblock не решают данную проблемму. Закрытие окон после каждой операции и остановка выполнения скриптов лишь заметляет засорение swap.
<p align="left">
Предполагается наличие утечки памяти в Selenium. 
</p>
<p align="left">
На рисунке показано поведение браузера с установленным расширением AddBlock. Видно как браузер открыл 17вкладок.
<img src="./Docs/Img /openWindows.jpg" width="100%">
</p>   
</p>
Для решения проблеммы переполнения swap был реализован механизм мониторинга, и перезапуска браузера при привышении лимита в 50Мб.<br>
Пики на рисунке приходятся на перезапуск браузера по превышению лимита swap. 
<img src="./Docs/Img /swapreboot.png" width="100%">

Но это до конца не решило проблемму. В некоторых случаях браузер может заполнить 1Гб swap за 30сек(мониторинг ведеться каждые 30сек).
<img src="./Docs/Img /serviceBroken.jpg" width="100%">
<p align="left">
Был добавлен механизм аварийного остановки браузера.
</p>
<p align="left">
Минимальная стабильная версия браузера Firefox,для работы с Selenium.WebDriver.GeckoDriver 0.30.0.1 -80.0. Драйвер ниже версии не умеет настраивать параметры профиля. Браузеры ниже версий после нескольких часов работы перестают работать.
<img src="./Docs/Img /dropfox.jpg" width="100%">
</p>
<p align="left">
В процессе длительного теста была выявлена еще одна ошибка браузера:
После 2часов 14 минут(8040с) при переходе на about:blank браузер всегда возвращает ошибку 
«TimedPromise timed out after»  и  нормально делает скрин. На скрине есть картинка одного и того же сайта. Очевидно какого-то старого, до ошибки. Нагрузка процессора не подымается выше 10%, процессы браузера и драйвера работают(команда top).
Для решения этой проблемы был добавлен FirefoxTimedPromiseBlankPageMonitor. В случае повторного повторения(2раза) данной ошибки при переходе на пустую страницу возвращается ошибка «Браузер вышел из строя». 
</p>

<h1 align="center">ScreenShotGenerator</h1>

<h2 align="center">Сервис создания скриншотов сайтов.</h2>

<p align="left">
Asp net.core 5 приложение.<br>
Точный дизайн сайта пока не определен.<br>
 Front часть содержит авторизацию по логину и соцсети Facebook. 
 <img src="./readme_assets/img1.png" width="100%">
</p>
<p align="left">
  Если войти под администратором, пользователь попадает на админ панель.
   <img src="./readme_assets/img2.png" width="100%">
  На ней отображаются графики нагрузки системы, и основные настройки:
    <img src="./readme_assets/img3.png" width="100%">
</p>  
  
 <h3 align="center">Вкладка "Кеш":</h3>
 <img src="./readme_assets/cache.png" width="100%">
<h3 align="center">Вкладка "Логи":</h3>
 <img src="./readme_assets/logs.png" width="100%">
<h3 align="center">Вкладка "Ошибки браузера":</h3>
 <img src="./readme_assets/browserErrors.png" width="100%">
<p align="left">
Для хранения данных используется БД Postgresql. Логи за каждый день хранятся в Logs/.
</p>
<p align="left">
Создание скринов выполняет браузер google chrome, управляемый Selenium. Для ускорения работы каждый браузер работает в отдельном потоке и получает информацию о задачах из пула задач.
Среднее время выполнение скриншотта на медленном соединении-3.5c, на быстром соединении может достигать 1с.
</p>

<h2>API сервиса.</h2>
<p align="left">
 Создать скрины по указанным адресам:<br>

 http://46.254.21.204//?url[0]=https://2ip.ru&url[1]=https://google.com&url[2]=https://yandex.com&allowedReferer=1

</p>
<p align="left">
Ответ json(расширенный пример):<br>

 [{"url":"https://2ip.ru","status":1,"path":"https://localhost:44350/imgCache/804adce908a159417a4650eb38d01e35.jpg","log":""},{"url":"https://gosterfaber789.com","status":0,"path":null,"log":"Exception in metod takeScreenShot:unknown error: net::ERR_NAME_NOT_RESOLVED\n (Session info: chrome=94.0.4606.81)"},{"url":"https://mail.ru","status":1,"path":"https://localhost:44350/imgCache/c4a87b0e73965dcb6586a78ca4b63901.jpg","log":""}]
</p>

<p align="left">
<h2>Тестирование.</h2>
Для тестирования сервиса используется net.core консольное приложение TestServices.
<img src="./readme_assets/testProgram.png" width="100%">
ПО считывает требуемое количество строк из файла, отправляет запрос на сервис, ждет окончания ответа. Измеряет время выполнения запроса.
Результат выполнения задачи сохраняется в базу данных sqlite, а так же в log файл.
</p>
<p align="left">
Пример мониторинга работы браузера с использование VNC и запущенного теста на VPS:
<img src="./readme_assets/vnc.png" width="100%">
</p>

Сервис протестирован на ОС CentOs 8 Streame установленной на VMware 
Workstation 16 Pro(1ядро 2 потока, 4гб памяти). При запуске 4-х браузеров, максимальное количество используемой памяти 1.8Гб.

 Ведется тестирование на VPS.

<p align="left">
<b>
Это закрытый проект. Исходные коды предоставлены исключительно для ознакомительных целей!
</b>
</p>

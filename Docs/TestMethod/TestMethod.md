<p align="left">
<h2>Тестирование.</h2>
Для тестирования сервиса используется консольное приложение TestServices(ведется разработка нового приложения).
<img src="./testProgram.png" width="100%">
ПО считывает требуемое количество строк из файла, отправляет запрос на сервис, ждет окончания ответа. Измеряет время выполнения запроса.
Результат выполнения задачи сохраняется в базу данных sqlite, а так же в log файл.
</p>
<p align="left">
Работа теста на VPS сервере. Используется один браузер.
<img src="./testProgram1.png" width="100%">
На 310 скринов затрачено 809секунд. В среднем один скрин делался 
2,6с. При работе двух браузеров время составляет 1,75с.
</p>
<p align="left">
Нагрузка VPS при работе одного браузера.Используется Netdata.
Характеристики VPS-2CPU,4Гб RAM.
<img src="./load.png" width="100%">
<img src="./load1.png" width="100%">
</p>
<p align="left">
Пример мониторинга работы браузера Chrome с использование VNC и запущенного теста на VPS:
<img src="./vnc.png" width="100%">
</p>

Сервис протестирован на ОС CentOs 8 Streame установленной на VMware 
Workstation 16 Pro(1ядро 2 потока, 4гб памяти). При запуске 4-х браузеров, максимальное количество используемой памяти 1.8Гб.

 Ведется тестирование на VPS.
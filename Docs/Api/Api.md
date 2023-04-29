<h2>API сервиса.</h2>
<p align="left">
 Создать скрины по указанным адресам:<br>

 http://46.254.21.204//?url[0]=https://2ip.ru&url[1]=https://google.com&url[2]=https://yandex.com&allowedReferer=1
</p>
<p align="left">
Ответ json(расширенный пример):<br>

 [{"url":"https://2ip.ru","status":1,"path":"https://localhost:44350/imgCache/804adce908a159417a4650eb38d01e35.jpg","log":""},{"url":"https://gosterfaber789.com","status":0,"path":null,"log":"Exception in metod takeScreenShot:unknown error: net::ERR_NAME_NOT_RESOLVED\n (Session info: chrome=94.0.4606.81)"},{"url":"https://mail.ru","status":1,"path":"https://localhost:44350/imgCache/c4a87b0e73965dcb6586a78ca4b63901.jpg","log":""}]
</p>
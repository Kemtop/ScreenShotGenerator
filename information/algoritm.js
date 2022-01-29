
var minCountBrowser = 2; // Минимальное кол-во запущенных браузеров
var maxCountBrowser = 5; // Максимальное кол-во запущенных браузеров
var averageTime = 30; // Среднее время выполнения запроса (по практике)

var realWorkBrowser = realWorkBrowser || minCountBrowser; // Реальное кол-во запущенных браузеров

/* Таблица задач 

  ID  STATUS    ADD_TIME   FINISH_TIME
  
  1   WAIT        2018        2048
  2   WAIT        2018        2048
  3   WAIT        2018        2048
  4   WAIT        2016        2046
  5   WAIT        2016        2046
  6   WAIT        2016        2046
  7   WAIT        2015        2045
  8   WAIT        2012        2042
  9   PROGRESS    2012        2042
  10  PROGRESS    2011        2041
  11  PROGRESS    2011        2041
  12  PROGRESS    2011        2041
  13  OK          2000        2030
  
  */
  
 // При поступлении очередного запроса мы проверяем, сможем ли мы его выполнить
 // task - массив урлов из запроса
  //аналог allowAcceptNewTasks
  function isCanAddTask(task){
	  
	  var countNewTask = task.length; // кол-во урлов в запросе (4)
	
    /*	
	  var finishTime = time() + 60; // Время на выполнение. time() вернул 2020, finishTime = 2080
	  var taskList = getFromTableTasks(); // Получаем массив задач из списка со статусом WAIT AND PROGRESS
	  */
	  
	/* Простой алгоритм */
	
	   //Количество задач ожидающих в пуле(статус 0);
	  var waitTaskCount = getCountFromWaitTasks(); // Получаем Количество задач из списка со статусом WAIT (8)
	  //Количество зачач который выполняют браузеры.
	  var progressTaskCount = getCountFromProgressTasks(); // Получаем Количество задач из списка со статусом PROGRESS (4)
	
	  var maxCountTaskForBrowser = 60/averageTime; // Макс. кол-во задач на браузер (2)
	  //countNewTask количество задач который мы хотим выполнять.
	  var taskCount = countNewTask + waitTaskCount + progressTaskCount; // Кол-во задач для будущего выполнения (16)
	  var needBrowserCount = taskCount/maxCountTaskForBrowser; // Кол-во браузеров для выполнения текущих задач (8)
	  
	   if(needBrowserCount > maxCountBrowser){ // Задачу добавить не можем, так как не справимся 
		     return false;
	            }
			  
	   if(needBrowserCount > realWorkBrowser){ // Проверяем необходимотсь запуска доп. браузеров
		      startBrowserCommand(needBrowserCount);//needBrowserCount-количество  браузеров которые должны работать в данный момент.
	              }
				  
	   if((realWorkBrowser - needBrowserCount) >= 2){ // Проверяем необходимотсь остановки доп. браузеров
	          
			  needBrowserCount = (needBrowserCount < minCountBrowser) ? minCountBrowser : (needBrowserCount + 1) 
		      stopBrowserCommand(needBrowserCount);//needBrowserCount-количество  браузеров которые должны работать в данный момент.
	              }
				  
		// можно Добавлять сайты в таблицу задач
		
	  return true; 
	  
	/* Конец Простого алгоритма */
	  
  }
  
  // Обработка HTTP-запроса 
  
  function htttp_response_handler(request){
	  
	  if(!is_array(request)){ // Для одиночного запроса, добавляем его в массив для дальнейшей универсальной обработки урлов
		  request = [request];
	        }
			
	   var response = [];
			
	    request.forEach(function(item, i){ // Заполняем заранее массив для ответа на запрос 
			response[i] = {
				        status: 2,
						url: item,
						data: null,
						log: "Сервис перегружен"
			           }	
		          });
				  
		if(!isCanAddTask(request)){ // Если сервис перегружен, отдаём массив с ответом
			  return response;
		        }
				
	// Добавляем сайты в список задач. 
	// Функция должна иметь возможность при создании очередного скрина менять данные в массиве response
				
		addTaskToTable(request, &response); 
		
    // Обработчик, проверяющий по интервалу выполнение всех задач в массиве ответа
				  
		var handlerInterval = setInterval(function(){
			
		// Если статус всех элементов массива изменился с 2 на любой другой, значит все элементы обработаны, возвращаем ответ
			if(response.every(function(item){  return (item.status !=2) ? true : false; }) {
				 clearInterval(handlerInterval); 
				 clearTimeout(handlerTimeout);
			       }
			
		    }, 1000);
			
	// Обработчик Таймаута через 58 секунд. 
	//  Если не были успешно обработаны все сайты, вернёт то что успело обработаться
			
	    var handlerTimeout = setTimeout(function(){  
			   clearInterval(handlerInterval);
			 return response;
		     }, 58000);
  }
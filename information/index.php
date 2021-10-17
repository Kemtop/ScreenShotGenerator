<?php  define("TEMPLATEPATH", $_SERVER['DOCUMENT_ROOT']); 
       define("_SCREENSHOT_MAIN", true);
	   
	     ini_set('display_errors','On');
          //  error_reporting(E_ALL);
	   
	     date_default_timezone_set("Europe/Kiev"); 
		   mb_internal_encoding("UTF-8");
		  include_once TEMPLATEPATH.'/include/functions.php'; 
		   
$option = (isset($_GET['task'])) ? $_GET['task'] : '';  

switch($option) {
 
 default:

  ///////////////////////////////////////// Sozdanie screenov /////////////////////////
	
	  $url = (isset($_GET['url'])) ? $_GET['url'] : '';
	  $test = (isset($_GET['test'])) ? true : false;
	  $timeout = (isset($_GET['timeout']) && !empty($_GET['timeout']) && is_numeric($_GET['timeout'])) ? $_GET['timeout'] : 6;
	  
	  $do_screen = false; 
	  $websites = array(); 
	  $task_result = array(); 
	  $i=0;
	  
	  $log = '';
	  $data = '';
	  
	   if(!is_array($url)) {
		     $url = array($url);
	      }
		  
	   if(!is_allowed_user_agent() && !isset($_GET['allowedReferer'])){
		 $message = "<pre>Ошибка! Неизвестный отправитель</pre>";
		   if($test){
			   echo $message;
		          } else {
			    logInTxt($message);
			   die($message);
			     }
	       }
		  
	/*** Первый круг, исключаем из списка сайтов несуществующие и пустые урлы, урлы ведущие на файлы ***/
	   
	     foreach ($url as $site) {
		 
		   $websites[$i] = array(
		                     'url'    => $site, 
							 'action' => 'do_screen', 
							 'data' => false
							 );
		 
		   $site = trim(urldecode($site)); 
		   
		     // Пустой урл
			   if(empty($site)) { 
					$log = 'This Url Empty!'; 
					add_website_data($websites[$i], $log);
					logInTxt($site." - ".$log); $i++; 
				   continue;
				   }
		   
		     $site = CleanStrFromDangerElements($site); 
			
             // Несуществующий урл			
			   if(!domain_exists($site, $test)) { 
					 $log = 'Unknown Url!'; 
					 add_website_data($websites[$i], $log); 
					 logInTxt($site." - ".$log); $i++; 
					continue;  
						   }
						   
		     $isFile = isFile($site);  
			 
			 // Урл, ведущий на файл
		       if($isFile) { 
			         add_website_data($websites[$i], false, 'return_isFile', $isFile); $i++; 
			       continue;
			     }
			
            $do_screen = true;			
		    $i++; 	  
	   }
	   
 /*** Конец первого круга ***/
	
   	
 /*** Если нет актуальных урлов, возвращаем обработанные ошибки ***/
 
    if(!$do_screen) {
		  returnRequestResult(($DISP = 5), $websites, $test);
	     exit;
	      } 
	   
	 $Display = action_for_display('set');
	 
 /*** Если нет свободных СкринСерверов, возвращаем обработанные ошибки ***/
			
     if($Display['response'] != 1) {
		  logInTxt("Error! No free servers.");
		     foreach($websites as $i=>$elem) {
			   if($elem['action'] == 'do_screen'){
				      $log = 'Please try again later!'; 
					  add_website_data($websites[$i], $log);
				          }
				   }
		     returnRequestResult(($DISP = 5), $websites, $test);
	      exit;
	    }

  /*** Готовим Подключение по SSH ***/
	
	$server = $Display['data'];
	$DISP = $server*10+5;
	$ssh = new Net_SSH2($_SERVER['HTTP_HOST']);
	
  /*** Если нет активного SSH подключения, возвращаем обработанные ошибки ***/
		 
      if(!$ssh->login(SSH_LOGIN, SSH_PASWORD)) { 
		   $log = 'SSH Access Denied!'; 
			logInTxt("Error! ".$log);
		   
		    foreach($websites as $i=>$elem) {
			   if($elem['action'] == 'do_screen'){
				      add_website_data($websites[$i], $log);
				          }
				     }
			returnRequestResult(($DISP = 5), $websites, $test);
	      exit;
		 }
		
  /* ------ Проверка запущенных процессов Xvfb ----------*/
  /* ------ Запуск Xvfb, если указанного процесса не найдено ----------*/
  /* ------ Проверка запущенных процессов Google Chrome ----------*/
		
	     $xvfb = is_expired_xvfb_process($DISP);
		         is_start_xvfb_process($DISP, $xvfb);
		         is_expired_chrome_process();
			  
			  
			  
   /* ------ 
        Конструктор основной команды 
	                          ---------- */
		
	 $command = ""; 
	 $step = 0;
		 
	   foreach($websites as $i=>$elem) { 
		  
		  if($elem['action'] != 'do_screen') {
			   continue;
		         } 
			 
		  $url = $elem['url'];
		  $screen_name = md5($DISP.$url).".jpg";
		  $screen_path = TEMPLATEPATH."/screen/".$screen_name;
			
		  if(file_exists($screen_path)) {
			    unlink($screen_path);
				     } 
					 
		  if($i > 5) {
			   $log = 'Sites Limit reached!'; 
				logInTxt($elem['url']." - ".$log);
				add_website_data($websites[$i], $log); 
			   continue; 
				} 
				
		 $websites[$i]['data'] = $screen_path;
		 $websites[$i]['display'] = $DISP;				 
			
		 $usr_dir = ".config/google-chrome/User-$DISP";
		   
	     $command .= ($step==0) ? "export" : " &&";
	     $command .= " DISPLAY=:$DISP && google-chrome --window-size=1280,1060 --user-data-dir=\"$usr_dir\" --window-position=0,0 --display=:$DISP --incognito";
	     $command .= " --disable-cache --disable-component-update --disable-desktop-notifications --disable-translate --enable-download-notification ";
	     $command .= " \"$url\" & sleep ".($timeout*($i+1))." && DISPLAY=:$DISP gm import -window root -crop 1260x965-0+60 -resize 300 $screen_path";		
		   
		 $step++;
		 
		 } /// END OF Конструктор основной команды  
		   
	 $result = $ssh->exec($command); 
	 $errorHeader = false;
	  sleep(1);	
		   
		    foreach($websites as $i=>$elem) {	
			  if($elem['action'] != 'do_screen'){
				     continue;
			           }
			 //// Если скрин не создан, отдаём ответ 204 
			 //// И на основном сервере отрабатываем ответ, игнорируя изображения
		      if(!file_exists($elem['data']) || filesize($elem['data']) == 711) {  
			      $log = 'Screen failed!'; 
				 if(!$errorHeader && !$test){
					  $errorHeader  = true;
					   http_response_code(204);
				        }
				   logInTxt($elem['url']." - DISPLAY=:$DISP ".$log, $result);
				   add_website_data($websites[$i], $log);
				      }
			     } 	
	
	/** Отдаём результирующее изображение запроса ***/
	
	     returnRequestResult($DISP, $websites, $test); 
			 
		$ssh->exec('exit'); 
	
	break;

    } ?>
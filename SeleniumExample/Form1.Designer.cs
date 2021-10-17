namespace SeleniumExample
{
    partial class Form1
    {
        /// <summary>
        /// Обязательная переменная конструктора.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Освободить все используемые ресурсы.
        /// </summary>
        /// <param name="disposing">истинно, если управляемый ресурс должен быть удален; иначе ложно.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Код, автоматически созданный конструктором форм Windows

        /// <summary>
        /// Требуемый метод для поддержки конструктора — не изменяйте 
        /// содержимое этого метода с помощью редактора кода.
        /// </summary>
        private void InitializeComponent()
        {
            this.button1 = new System.Windows.Forms.Button();
            this.buttonRunInBG = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(281, 164);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(247, 57);
            this.button1.TabIndex = 0;
            this.button1.Text = "Создать скриншот";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // buttonRunInBG
            // 
            this.buttonRunInBG.Location = new System.Drawing.Point(76, 38);
            this.buttonRunInBG.Name = "buttonRunInBG";
            this.buttonRunInBG.Size = new System.Drawing.Size(670, 74);
            this.buttonRunInBG.TabIndex = 1;
            this.buttonRunInBG.Text = "Запустить в фоновом режиме";
            this.buttonRunInBG.UseVisualStyleBackColor = true;
            this.buttonRunInBG.Click += new System.EventHandler(this.buttonRunInBG_Click);
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(339, 366);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(179, 63);
            this.button2.TabIndex = 2;
            this.button2.Text = "Выйти";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(11F, 24F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.buttonRunInBG);
            this.Controls.Add(this.button1);
            this.Name = "Form1";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Form1";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button buttonRunInBG;
        private System.Windows.Forms.Button button2;
    }
}


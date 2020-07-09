using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Timers;
using System.IO.Ports;
using System.Threading;

namespace Sim
{
	/// <summary>
	/// MainWindow.xaml 的交互逻辑
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow( )
		{
			InitializeComponent ( );
			for(int index = 0;index < PationOnoffStatus.Length; index++) {
				PationOnoffStatus[ index ] = true;
				PationRegisterStatus[ index ] = true;
			}
			for(int index = 0;index < AmpRegisterStatus.Length; index++) {
				AmpRegisterStatus[ index ] = true;
			}
		}

		//定时器，用于循环显示串口接收到的数据
		System.Timers.Timer timer;
		CancellationTokenSource cts = new CancellationTokenSource();
		SerialPort serialPort;
		string serial_name = string.Empty;
		int serial_baudrate = 0;
		int last_received_count = 0;

		bool[] PationOnoffStatus = new bool[ 128 ];
		bool[] PationHiddenStatus = new bool[ 128 ];
		bool[] PationErrorStatus = new bool[ 128 ];
		bool[] PationRegisterStatus = new bool[ 128 ];
		bool[] PationSpeakerErrorStatus = new bool[ 128 ];
		bool[] AmpRegisterStatus = new bool[ 21 ];
		bool[] AmpHiddenStatus = new bool[ 21 ];
		bool[,] AmpWorkingStatus = new bool[ 21, 4 ];

		private void CheckBox_KeyDown(object sender, KeyEventArgs e)
		{
			if (!((e.Key == Key.Enter) || (e.Key == Key.Up) || (e.Key == Key.Down) || (e.Key == Key.Tab))) {
				e.Handled = true;
				return;
			}

			if (e.Key == Key.Enter) {
				CheckBox chk = sender as CheckBox;
				if (( bool )chk.IsChecked == false) {
					chk.IsChecked = true;
				} else {
					chk.IsChecked = false;
				}
			}
		}

		private void RadioButton_KeyDown(object sender, KeyEventArgs e)
		{
			if (!((e.Key == Key.Enter) || (e.Key == Key.Up) || (e.Key == Key.Down) || (e.Key == Key.Tab))) {
				e.Handled = true;
				return;
			}

			if (e.Key == Key.Enter) {
				RadioButton rdb = sender as RadioButton;
				if (( bool )rdb.IsChecked == false) {
					rdb.IsChecked = true;
					//刷新对应的细节状态标志
					for (int index = 0; index < AmpHiddenStatus.Length; index++) {
						if(wrpAmpSetting_stuaus.Children[index] == sender) {
							for(int index_1 = 0; index_1 < 4; index_1++) {

								CheckBox chk = wrpAmpSetting_stuaus.Children[ index_1 + 21 ] as CheckBox;
								if (AmpWorkingStatus[ index, index_1 ]) {
									chk.IsChecked = true;
								} else {
									chk.IsChecked = false;
								}
							}
						}
					}
				} 
			}
		}

		private void CobSerialport_PreviewMouseDown(object sender, MouseButtonEventArgs e)
		{
			ComboBox cob = sender as ComboBox;
			/*获取当前电脑上的串口集合，并将其显示在cob控件中*/
			string[] SerialPortlist = SerialPort.GetPortNames();
			cob.Items.Clear();
			foreach (string name in SerialPortlist) {
				SerialPort serialPort = new SerialPort( name, 9600, Parity.Mark, 8, StopBits.One );
				try {
					serialPort.Open();
					serialPort.Close();
					cob.Items.Add( name );
				} catch {
					;
				}
			}
		}

		private void CobSerialport_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			ComboBox cob = sender as ComboBox;
			if (cob.SelectedIndex < 0) {
				serial_name = string.Empty;
				return;
			}

			serial_name = cob.SelectedItem.ToString();
			if (timer != null) {
				timer.Enabled = false; //暂时不使用定时器循环检查
			}
			if (serialPort != null) {
				if (serialPort.IsOpen) {
					serialPort.Close();
				}
			}
		}

		private void CobBaudrate_PreviewMouseDown(object sender, MouseButtonEventArgs e)
		{
			ComboBox cob = sender as ComboBox;
			cob.Items.Clear();
			cob.Items.Add( "1200" );
			cob.Items.Add( "2400" );
			cob.Items.Add( "4800" );
			cob.Items.Add( "9600" );
			cob.Items.Add( "19200" );
			cob.Items.Add( "38400" );
			cob.Items.Add( "57600" );
			cob.Items.Add( "115200" );
		}

		private void CobBaudrate_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			ComboBox cob = sender as ComboBox;
			if (cob.SelectedIndex < 0) {
				serial_baudrate = 0;
				return;     
			}

			serial_baudrate = Convert.ToInt32( cob.SelectedItem );
		}

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			//串口初始化、定时器使能（检查分区及功放状态）
			if ((serial_name == string.Empty) || (serial_baudrate == 0)) {
				MessageBox.Show( "请选择正常的串口名及波特率" );
				return;
			} else {
				//实例化一个串口对象  用于仿真和通讯转接板之间的通讯
				serialPort = new SerialPort( serial_name, serial_baudrate, Parity.None, 8, StopBits.One );
				serialPort.Open();
			}

			if (timer == null) {
				//开启定时器，用于实时刷新进度条、测试环节、测试项、测试值
				timer = new System.Timers.Timer( 20 );   //实例化Timer类，设置间隔时间单位毫秒
				timer.Elapsed += new ElapsedEventHandler( UpdateWork ); //到达时间的时候执行事件；     
				timer.AutoReset = true;   //设置是执行一次（false）还是一直执行(true)；     
				timer.Enabled = true;     //是否执行System.Timers.Timer.Elapsed事件； 
			} else {
				timer.Enabled = true; //允许重新使能定时器检测
			}
		}

		private delegate void dlg_CheckedstatusSet( int index, WrapPanel wrapPanel, bool checked_status, bool is_checkbox );
		private delegate object dlg_CheckedstatusGet(int index, WrapPanel wrapPanel, bool is_checkbox);

		object CheckedstatusGet(int index ,WrapPanel wrapPanel ,bool is_checkbox)
		{
			if (is_checkbox) {
				CheckBox chk = wrapPanel.Children[ index ] as CheckBox;
				return chk.IsChecked;
			} else {
				RadioButton rdb = wrpAmpSetting_stuaus.Children[ index ] as RadioButton;
				return rdb.IsChecked;
			}
		}

		void CheckedstatusSet( int index, WrapPanel wrapPanel, bool checked_status , bool is_checkbox )
		{
			if ( is_checkbox ) {
				CheckBox chk = wrapPanel.Children [ index ] as CheckBox;
				chk.IsChecked = checked_status;
			} else {
				RadioButton rdb = wrpAmpSetting_stuaus.Children [ index ] as RadioButton;
				rdb.IsChecked = checked_status;
			}
		}

		/// <summary>
		/// 定时器中执行委托用于显示实时情况
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void UpdateWork(object sender, ElapsedEventArgs e)
		{
			try {
				if (!cts.IsCancellationRequested) {
					object obj;
					for (int index = 0; index < PationOnoffStatus.Length; index++) {
						obj = Dispatcher.Invoke( new dlg_CheckedstatusGet( CheckedstatusGet ), index, wrpOnoff_stuaus, true );
						PationOnoffStatus[ index ] = ( bool )obj;
					}

					for (int index = 0; index < PationErrorStatus.Length; index++) {
						obj = Dispatcher.Invoke( new dlg_CheckedstatusGet( CheckedstatusGet ), index, wrpHidden_stuaus, true );
						PationHiddenStatus[ index ] = ( bool )obj;
					}

					for (int index = 0; index < PationErrorStatus.Length; index++) {
						obj = Dispatcher.Invoke( new dlg_CheckedstatusGet( CheckedstatusGet ), index, wrpError_stuaus, true );
						PationErrorStatus[ index ] = ( bool )obj;
					}

					for (int index = 0; index < PationRegisterStatus.Length; index++) {
						obj = Dispatcher.Invoke( new dlg_CheckedstatusGet( CheckedstatusGet ), index, wrpReigster_stuaus, true );
						PationRegisterStatus[ index ] = ( bool )obj;
					}

					for (int index = 0; index < PationSpeakerErrorStatus.Length; index++) {
						obj = Dispatcher.Invoke( new dlg_CheckedstatusGet( CheckedstatusGet ), index, wrpSpeakerError_stuaus, true );
						PationSpeakerErrorStatus[ index ] = ( bool )obj;
					}

					for (int index = 0; index < AmpRegisterStatus.Length; index++) {
						obj = Dispatcher.Invoke( new dlg_CheckedstatusGet( CheckedstatusGet ), index, wrpAmpResigster_stuaus, true );
						AmpRegisterStatus[ index ] = ( bool )obj;
					}

					for (int index = 0; index < AmpHiddenStatus.Length; index++) {
						obj = Dispatcher.Invoke( new dlg_CheckedstatusGet( CheckedstatusGet ), index, wrpAmpHidden_stuaus, true );
						AmpHiddenStatus[ index ] = ( bool )obj;
					}

					for (int index = 0; index < AmpHiddenStatus.Length; index++) {
						obj = Dispatcher.Invoke( new dlg_CheckedstatusGet( CheckedstatusGet ), index, wrpAmpSetting_stuaus, false );
						if (( bool )obj) {
							for (int index_1 = 21; index_1 < 25; index_1++) {
								obj = Dispatcher.Invoke( new dlg_CheckedstatusGet( CheckedstatusGet ), index_1, wrpAmpSetting_stuaus, true );
								AmpWorkingStatus[ index, (index_1 - 21) ] = ( bool )obj;
							}
						}
					}

					//以下进行串口数据的应答操作
					if((serialPort.BytesToRead != 0) && (last_received_count == serialPort.BytesToRead)) {
						CheckReceivedData();
					}
					last_received_count = serialPort.BytesToRead;
				}
			} catch (Exception ex) {
				MessageBox.Show( ex.ToString() );
			}
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			if(timer != null) { timer.Enabled = false; }

			if(serialPort != null) { serialPort.Close();serialPort.Dispose(); }
			cts.Cancel();
		}

		delegate object dlg_txtValueGet( TextBox textBox );
		delegate void dlg_txtValueShow( TextBox textBox, string value );
		void txtValueShow( TextBox textBox, string value )
		{
			textBox.Text = value;
		}

		object txtValueGet(TextBox textBox )
		{
			return textBox.Text;
		}

		private void CheckReceivedData()
		{
			byte[] temp = new byte[serialPort.BytesToRead];
			int index_of_header = 0;
			for(index_of_header = 0; index_of_header < temp.Length; index_of_header++) {
				if(temp[ index_of_header] == 0x68) {
					break;
				}
			}

			byte[] received_data = new byte[ temp[ index_of_header + 3 ] + 6 ];
			int max_count = received_data.Length;
			if((temp.Length - index_of_header) < received_data.Length) {
				max_count = (temp.Length - index_of_header);
			}
			Buffer.BlockCopy( temp, index_of_header, received_data, 0, max_count );

			byte [ ] send_data = new byte [ 20 ];
			send_data [ 0 ] = 0x68;

			switch (( Soundsource.soundsource_cmd )received_data[ 3 ]) {
				case Soundsource.soundsource_cmd.Cmd_EmergencyControl:
					Dispatcher.Invoke ( new dlg_txtValueShow ( txtValueShow ), txtEmergencyStatus, received_data [ 4 ].ToString ( ) );					
					send_data [ 1 ] = 0xff;
					send_data [ 3 ] = 0x01;
					send_data [ 4 ] = 0x00;
					break;
				case Soundsource.soundsource_cmd.Cmd_PationOnoffControl:
					int loc = 0x01;
					for(int index = received_data[4] ;index <= received_data[5] ; index++ ) {
						
					}
					break;
				case Soundsource.soundsource_cmd.Cmd_PationHiddenControl:
					break;
				case Soundsource.soundsource_cmd.Cmd_PationOnoffQuery:
					break;
				case Soundsource.soundsource_cmd.Cmd_PationHiddenQuery:
					break;
				case Soundsource.soundsource_cmd.Cmd_PationErrorQuery:
					break;
				case Soundsource.soundsource_cmd.Cmd_BeepErase:
					break;
				case Soundsource.soundsource_cmd.Cmd_PationAllControl:
					break;
				case Soundsource.soundsource_cmd.Cmd_PationErrorControl:
					break;
				case Soundsource.soundsource_cmd.Cmd_FlashAddressSet:
					break;
				case Soundsource.soundsource_cmd.Cmd_FlashDataWrite:
					break;
				case Soundsource.soundsource_cmd.Cmd_FlashDataRead:
					break;
				case Soundsource.soundsource_cmd.Cmd_WorkingStatusQuery:
					break;
				case Soundsource.soundsource_cmd.Cmd_PationAddressSet:
					break;
				case Soundsource.soundsource_cmd.Cmd_PationWatchRightsSet:
					break;
				case Soundsource.soundsource_cmd.Cmd_SelfCheck:
					break;
				case Soundsource.soundsource_cmd.Cmd_PationRegisterControl:
					break;
				case Soundsource.soundsource_cmd.Cmd_PationRegisterQuery:
					break;
				case Soundsource.soundsource_cmd.Cmd_SpeakerErrorQuery:
					break;
				case Soundsource.soundsource_cmd.Cmd_SpeakerErrorControl:
					break;
				case Soundsource.soundsource_cmd.Cmd_Reset:
					break;
				case Soundsource.soundsource_cmd.Cmd_AmpStatusQuery:
					break;
				case Soundsource.soundsource_cmd.Cmd_BaudrateControl:
					break;
				case Soundsource.soundsource_cmd.Cmd_AmpRegisterControl:
					break;
				case Soundsource.soundsource_cmd.Cmd_AmpRegisterQuery:
					break;
				case Soundsource.soundsource_cmd.Cmd_AmpHiddenControl:
					break;
				case Soundsource.soundsource_cmd.Cmd_AmpHiddenQuery:
					break;
				default:break;
			}

		}

	}
}

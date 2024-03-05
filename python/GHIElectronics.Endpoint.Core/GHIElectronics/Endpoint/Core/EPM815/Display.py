import os, mmap
import subprocess
import time
import GHIElectronics.Endpoint.Core.EPM815.Gpio as Gpio
   


__InitializedList = []

class DisplayConfiguration(object):
    def __init__(self): 
        self.Clock= 0
        self.Hsync_start= 0
        self.Hsync_end= 0
        self.Htotal= 0
        self.Vsync_start= 0
        self.Vsync_end= 0
        self.Vtotal= 0
        self.Num_modes=  1
        self.Dpi_width= 0
        self.Dpi_height= 0
        self.Bus_flags= 0
        self.Bus_format= 0
        self.Connector_type= 0
        self.Bpc=  8
        self.Width = 0
        self.Height = 0
        


class DisplayController(object):
    __InitializeCount = 0
    __FBHandle = -1
    __Stride = -1
    __FBWidth = -1
    __FBHeight = -1
    __FBSize = -1
    __FBPtr = -1

    def __init__(self, configuration: DisplayConfiguration): 
        self.configuration = configuration

        self.__Acquire()

    def __Acquire(self):
        if DisplayController.__InitializeCount == 0:
            self.__LoadResource()

        DisplayController.__InitializeCount +=1

    def __Release(self):
        DisplayController.__InitializeCount -=1
        if DisplayController.__InitializeCount == 0:
            self.__UnLoadResource()

        

    def __LoadResource(self):
        setting = f"{self.configuration.Clock},"
        setting += f"{self.configuration.Width},{self.configuration.Hsync_start},{self.configuration.Hsync_end},{self.configuration.Htotal},"
        setting += f"{self.configuration.Height},{self.configuration.Vsync_start},{self.configuration.Vsync_end},{self.configuration.Vtotal},"
        setting += f"{self.configuration.Num_modes},{self.configuration.Dpi_width},{self.configuration.Dpi_height},{self.configuration.Bus_flags},{self.configuration.Bus_format},{self.configuration.Connector_type},{self.configuration.Bpc}"

        #subprocess.run(["modprobe", f"panel-simple ltdc_generic_setting={setting}"]) 
        subprocess.check_call(['modprobe', 'panel-simple', f'ltdc_generic_setting={setting}']) 

        while (True):
            if os.path.exists("/dev/fb0") == True:
                break

            time.sleep(0.01)

        if (DisplayController.__FBHandle < 0):
            DisplayController.__FBHandle = os.open("/dev/fb0", os.O_RDWR | os.O_SYNC)

        if (DisplayController.__Stride < 0):
            fb_stride = open("/sys/class/graphics/fb0/stride", "r")
            fb_stride.seek(0,0) 
            buf_read = fb_stride.read(16)

            fb_stride.close()

            DisplayController.__Stride = int(buf_read)

            DisplayController.__FBWidth = DisplayController.__Stride >> 1
            DisplayController.__FBHeight = self.configuration.Height
            DisplayController.__FBSize = DisplayController.__FBWidth * DisplayController.__FBHeight *  2

        if (DisplayController.__FBPtr == -1):
            DisplayController.__FBPtr = mmap.mmap(DisplayController.__FBHandle, DisplayController.__FBSize, mmap.MAP_SHARED, mmap.PROT_WRITE | mmap.PROT_READ, offset = 0)

        while (True):
            if os.path.exists("/sys/class/graphics/fbcon/cursor_blink") == True:
                break

            time.sleep(0.01)

        subprocess.run(["ghi_disable_cursor.sh", ""]) 

    def __UnLoadResource(self):
        # TODO:
        return
    
    def Flush(self, data: bytearray, offset: int, length: int, width: int, height: int):
        widthDest2 = DisplayController.__FBWidth << 1
        widthSrc2 = width << 1

        y_min = min(height, self.configuration.Height)

        x_min = min(widthDest2, widthSrc2)

        x_min = min(x_min, self.configuration.Width << 1)

        for y in range(0,y_min):             
            indexSrc = y * height * 2

            line = data[indexSrc: indexSrc + x_min]

            indexDest = widthDest2 * y
            DisplayController.__FBPtr.seek(indexDest, 0)
            DisplayController.__FBPtr.write(line)

        



        


            


    
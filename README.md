ASCOM Camera driver for Lumix

 I Purpose

This driver provides an interface to the Lumix http over wifi remote control protocol
in order to present lumix cameras as ASCOM cameras and be used by astro photo SW like APT or Indi 
The camera believes that it is connected to the Panasonic ImageApp

Driver has been tested with the G80, GH$ and GH5s but shouldwork with all Wifi Lumix.

16MP sensor was the prototype. Now it is possible to work with a range of 
 - 10M (GH5S) 
 - 12MP(GH1) 
 - 16MP (GH4, G80)
 - 20MP (GH5, G9 etc).
 
II Usage

To connect to the camera:
1) On the camera (similar to what is needed with the Panasonic ImageApp)
	a) set it to "M"
	b) connect to a wifi network (best if local hotspot_
	c) Camera waits for an app to connect 
	d) best if camera is set to manual focus too.
	e) make sure there is an SD card too. (this error is not handled yet)
2) on the PC
	a) launch the Imaging SW (e.g. APT)
	b) chose the LumixG80 Ascom from the chooser window
	c) click properties
	d) the driver will look for the Lumix camera on the local wifi network and connect to it (the camera should say "under remote control")
	e) set the ISO, Speed and Transfer mode (JPG, Thumb or Raw): read below for details
   f) select the correct resolution for your camera. I hope to make it "discoverable" soon)  
	g) Temp folder to store the file from the camera.
	h) Path to the DCraw.exe file that is required to deal with the RAW file from the camera. This File is distributed with the setup and should be in 
C:\Program Files (x86)\Common Files\ASCOM\Camera
	i) hit ok.

The driver allows to set the speed,iso and format (RAW or RAW+JPG) of the camera  
transfers the image (Raw or JPG) on the PC and exposes the image array in RGB.

It relies on DCRaw to handle the Raw format, or the native VB.NET imaging for JPG
Images are then translated into Tiff and then passed to the image array.

RAW would be preferred but the file is substantially larger and therefore longer to tranfer.
therefore the download is often interrupted. the driver tries to recover/continue the DL but it does not always works
this leaves with an incomplete RAW file that is still passed on but not ideal. 

Given the longer transfer time it substantially cuts into the active shooting since all this process is sequential
So if you have a 1mn exposure and it takes 40s to get it onto your driver that is 40s you are not shooting...

Hence the jpg transfer option. file is smaller and transfer faster and should still be valuable for the Astro SW.
in any case the camera keeps the RAW or the RAW+jpg on the SD card and the Astro SW should have a fits file from the driver.
the transfered files (jpg or raw) and intermediary tiff files are deleted as soon as needed in order to save disk space.
code is quite nasty and could use some factoring into further utility classes/methods etc.

I added a "thumb" transfer mode ehich takes a large thumbnail of the image  (1440x1080) in order to further reduce the trnasfer size. 
 not sure if this helps much and if it will screw up the platesolving since now resolution is different from the actual sensor size. 
in this case though the pixelpitch is changed in the driver so to help in that process.

Lastly there is still a pending issue with 14 bit RAWs fram GH5s. this has to do with DCRaw that does not yet handle this format. 
for that reason it is best for GH5s to use JPG format

III Installation:
for windows 32 and 64 bit.
download and run ASCOM.LumixG80.Camera Setup.exe. 


 Implements:	ASCOM Camera interface version: 1.0
 Author:		robert hasson robert_hasson@yahoo.com
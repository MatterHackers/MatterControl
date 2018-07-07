#include <stdlib.h>
#include <stdio.h>
#include <fcntl.h>
#include <errno.h>
#include <string.h>
#include <linux/termios.h>

int ioctl(int d, int request, ...);

int set_baud (char *devfile, int baud);

int set_baud(char *devfile, int baud)
{

#ifndef __linux__
  return 0; 
#endif

  struct termios2 t;
  int fd;

  fd = open(devfile, O_RDWR | O_NOCTTY | O_NDELAY);

  if (fd == -1)
      {
        fprintf(stderr, "error opening %s: %s", devfile, strerror(errno));
        return 2;
      }

  if (ioctl(fd, TCGETS2, &t))
    {
      perror("TCGETS2");
      return 3;
    }
	
  printf("SetSerial: Reported speed before update %d\n", t.c_ospeed);

  t.c_cflag &= ~CBAUD;
  t.c_cflag |= BOTHER;
  t.c_ispeed = baud;
  t.c_ospeed = baud;

  if (ioctl(fd, TCSETS2, &t))
    {
      perror("TCSETS2");
      return 4;
    }

  if (ioctl(fd, TCGETS2, &t))
    {
      perror("TCGETS2");
      return 5;
    }


  //close(fd);

  printf("SetSerial: Reported speed after update %d\n", t.c_ospeed);
  return 0;
}

import { Injectable, Inject, OnInit } from '@angular/core';
import * as moment from 'moment';
import { API_URL } from '../app-injection-tokens';

@Injectable({
  providedIn: 'root'
})
export class AudioService{

  constructor(@Inject(API_URL) private apiUrl: string) {
  }

  audioObj = new Audio();

  currentTime = '00:00';
  duration='00:00';
  delay;
  isPlaying = false;
  audioName = "";
  idMusic: number=-1;
  currentAudioFileName: string = null;

  openFile(idM, filename, name) {
    this.duration = '00:00';
    this.currentAudioFileName = filename;
    this.idMusic = idM;
    this.audioName = name;
    this.audioObj.src = `${this.apiUrl}api/music/DownloadFile/${filename}`;
    this.audioObj.preload = "auto"
    this.audioObj.load();
    this.play();
    this.updateProgress();
  }

  play() {
    if (this.audioObj.src) {
      this.audioObj.play();
      if (this.currentAudioFileName)
        this.isPlaying = true;
    }
  }

  clearMusic() {
    this.pause();
    clearTimeout(this.delay);
    this.audioObj = new Audio();
    this.audioName = "";
    this.duration = "00:00";
    this.currentTime = "00:00";
  }

  pause() {
    this.audioObj.pause();
    if (this.currentAudioFileName)
      this.isPlaying = false;
  }

  stop() {
    this.audioObj.pause();
    this.audioObj.currentTime = 0;
  }

  timeFormat(time, format = "mm:ss") {
    const momentTime = time * 1000;
    return moment.utc(momentTime).format(format);
  }

  mouseDown() {
    clearTimeout(this.delay);
    this.audioObj.pause();
  }

  mouseUp(ev) {
    var value = ev.target.value;
    var parsed = parseFloat(value);
    this.audioObj.currentTime = parsed;
    this.audioObj.play();
    this.updateProgress();
  }

  updateProgress() {
    this.currentTime = this.timeFormat(this.audioObj.currentTime);
    if (this.timeFormat(this.audioObj.duration)!='Invalid date') {
      this.duration = this.timeFormat(this.audioObj.duration);
    }
    this.delay = setTimeout(() => {
      this.updateProgress();
    }, 1000);
  }

  mute() {
    this.audioObj.muted = true;
    this.audioObj.volume = 0;
  }

  unmute() {
    this.audioObj.muted = false;
  }

  changeVolume() {
    this.unmute();
  }
}

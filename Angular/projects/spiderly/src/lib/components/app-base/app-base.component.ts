import { Component, OnInit } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { SPIDERLY_FAVICON_PATH } from '../../../public-api';

@Component({
  template: ''
})
export class AppBaseComponent implements OnInit {
  constructor(private titleService: Title) {}

  ngOnInit() {
    this.initializeFavicon();
  }

  private initializeFavicon() {
    const link = document.querySelector("link[rel*='icon']") || document.createElement('link');
    link.type = 'image/png';
    link.rel = 'icon';
    link.href = SPIDERLY_FAVICON_PATH;
    document.getElementsByTagName('head')[0].appendChild(link);
  }
} 
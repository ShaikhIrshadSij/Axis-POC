import { Component, Inject, type OnInit, type OnDestroy, type ElementRef, ViewChild } from "@angular/core"
import { MAT_DIALOG_DATA, MatDialogModule } from "@angular/material/dialog"
import Hls from "hls.js"
import { VideoStreamService } from '../../services/video-stream.service';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { CommonModule } from '@angular/common';

@Component({
  selector: "app-video-modal",
  templateUrl: "./video-modal.component.html",
  standalone: true,
  imports: [
    MatCardModule,
    MatDialogModule,
    MatButtonModule,
    CommonModule
  ],
  styleUrls: ["./video-modal.component.scss"],
})
export class VideoModalComponent implements OnInit, OnDestroy {
  @ViewChild("videoPlayer", { static: true }) videoPlayer!: ElementRef<HTMLVideoElement>

  private hls: Hls | null = null;

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: { cameraId: string },
    private videoStreamService: VideoStreamService
  ) { }

  ngOnInit() {
    this.loadStream()
  }

  ngOnDestroy() {
    this.destroyStream()
  }

  private loadStream() {
    this.videoStreamService.getFullVideoStream(this.data.cameraId).subscribe(
      (blob) => {
        const videoUrl = URL.createObjectURL(blob)
        if (Hls.isSupported()) {
          this.hls = new Hls()
          this.hls.loadSource(videoUrl)
          this.hls.attachMedia(this.videoPlayer.nativeElement)
        } else if (this.videoPlayer.nativeElement.canPlayType("application/vnd.apple.mpegurl")) {
          this.videoPlayer.nativeElement.src = videoUrl
        }
      },
      (error) => console.error("Error loading video stream:", error),
    )
  }

  private destroyStream() {
    if (this.hls) {
      this.hls.destroy()
      this.hls = null
    }
  }
}

